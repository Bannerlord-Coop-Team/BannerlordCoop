using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using System.Linq;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Client-side deferred settlement-taken transition for siege captures. The prompt arrives before the old map event
/// is destroyed, so a mission or simulation scoreboard must release its presentation before the aftermath menu opens.
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

    /// <summary>Parks a settlement-taken transition until the battle presentation has finished tearing down.</summary>
    internal static void Arm(MobileParty leaderParty, Settlement settlement)
    {
        pendingLeaderParty = leaderParty;
        pendingSettlement = settlement;
    }

    internal static bool IsBattlePresentationActive()
    {
        if (MissionState.Current != null) return true;

        return Game.Current?.GameStateManager?.GameStates
            .OfType<MapState>()
            .Any(state => state.IsSimulationActive) == true;
    }

    internal static bool TryTakeReady(out MobileParty leaderParty, out Settlement settlement)
    {
        leaderParty = null;
        settlement = null;
        if (pendingSettlement == null || IsBattlePresentationActive()) return false;

        leaderParty = pendingLeaderParty;
        settlement = pendingSettlement;
        pendingLeaderParty = null;
        pendingSettlement = null;
        return true;
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer) return;
        if (!TryTakeReady(out var leaderParty, out var settlement)) return;

        siegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        pendingLeaderParty = null;
        pendingSettlement = null;
    }
}
