using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Client-side deferred settlement-taken transition for a real-time siege capture. When the capturing player wins
/// the assault in a live mission, the server captures the town and destroys the map event, but the client's mission
/// pops back to the STALE pre-mission siege "encounter" menu instead of the aftermath menu (native
/// <c>PlayerEncounter.DoEnd</c> can't run: MainParty is no longer attached to the destroyed map event). The prompt
/// reaches <see cref="ISiegeEventInterface.PromptLocalAftermathChoice"/> while the mission is still tearing down,
/// which is too early to touch PlayerEncounter, so it parks the transition here. This retries it on the next
/// CampaignTick once the mission has fully popped, mirroring PvPInteractionClientHandler's deferred-close pattern.
/// </summary>
internal class SiegeCaptureTransitionRetryHandler : IHandler
{
    // Game-thread only (Arm runs inside the prompt's GameThread closure; Handle_CampaignTick runs on the campaign
    // tick), so plain statics are safe. Static so Arm is reachable from SiegeEventInterface without a back-reference.
    private static MobileParty pendingLeaderParty;
    private static Settlement pendingSettlement;

    private readonly IMessageBroker messageBroker;
    private readonly ISiegeEventInterface siegeEventInterface;

    public SiegeCaptureTransitionRetryHandler(IMessageBroker messageBroker, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.siegeEventInterface = siegeEventInterface;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    /// <summary>Parks a settlement-taken transition to run once the battle mission has finished tearing down.</summary>
    internal static void Arm(MobileParty leaderParty, Settlement settlement)
    {
        pendingLeaderParty = leaderParty;
        pendingSettlement = settlement;
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer) return;
        if (pendingSettlement == null) return;
        // The mission screen is still up (or popping): re-establishing PlayerEncounter now is unsafe. Wait.
        if (MissionState.Current != null) return;

        var leaderParty = pendingLeaderParty;
        var settlement = pendingSettlement;
        pendingLeaderParty = null;
        pendingSettlement = null;

        siegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        pendingLeaderParty = null;
        pendingSettlement = null;
    }
}
