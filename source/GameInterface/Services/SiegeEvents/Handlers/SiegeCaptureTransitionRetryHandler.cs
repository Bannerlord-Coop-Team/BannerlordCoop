using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Client-side deferred settlement-taken transition for siege captures. The prompt arrives before the old map event
/// is destroyed, so the battle presentation and the post-battle loot steps must finish before the aftermath menu opens.
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

    /// <summary>Parks a settlement-taken transition until the battle has finished resolving.</summary>
    internal static void Arm(MobileParty leaderParty, Settlement settlement)
    {
        pendingLeaderParty = leaderParty;
        pendingSettlement = settlement;
    }

    /// <summary>True while the battle presentation or the post-battle loot steps still own the encounter.</summary>
    internal static bool IsBattleStillResolving() => IsBattlePresentationActive() || IsAfterBattleEncounterActive();

    private static bool IsBattlePresentationActive()
    {
        if (MissionState.Current != null) return true;

        return Game.Current?.GameStateManager?.GameStates
            .OfType<MapState>()
            .Any(state => state.IsSimulationActive) == true;
    }

    // The encounter's own End routes the leader once its loot steps finish, like vanilla DoEnd.
    private static bool IsAfterBattleEncounterActive()
    {
        switch (Campaign.Current?.PlayerEncounter?.EncounterState)
        {
            case PlayerEncounterState.PlayerVictory:
            case PlayerEncounterState.CaptureHeroes:
            case PlayerEncounterState.FreeHeroes:
            case PlayerEncounterState.LootParty:
            case PlayerEncounterState.LootInventory:
            case PlayerEncounterState.LootShips:
            case PlayerEncounterState.End:
                return true;
            default:
                return false;
        }
    }

    internal static bool TryTakeReady(out MobileParty leaderParty, out Settlement settlement)
    {
        leaderParty = null;
        settlement = null;
        if (pendingSettlement == null || IsBattleStillResolving()) return false;

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
        // The encounter End may have opened the menu and the player already picked, which releases the hold.
        if (!SiegeCaptureMenuHoldPatch.IsHeld(settlement)) return;

        siegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement, previousOwnerClan: null);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        pendingLeaderParty = null;
        pendingSettlement = null;
    }
}
