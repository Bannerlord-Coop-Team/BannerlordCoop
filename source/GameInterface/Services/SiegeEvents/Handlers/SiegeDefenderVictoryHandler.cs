using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Client-side deferred siege-defeated transition for a winning inside defender. On a defender assault victory
/// the server replicates the SiegeEvent/MapEvent teardown, which bypasses vanilla's local siege-end routing, so
/// the winner would otherwise fall through to the settlement arrival menu. The server sends
/// <see cref="NetworkPromptSiegeDefenderVictory"/> after the finalize; this parks the transition and runs it on
/// the next CampaignTick once the mission has fully popped, mirroring <see cref="SiegeCaptureTransitionRetryHandler"/>.
/// </summary>
internal class SiegeDefenderVictoryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    // Game-thread only (armed inside a GameThread closure, drained on CampaignTick).
    private Settlement pendingSettlement;

    public SiegeDefenderVictoryHandler(IMessageBroker messageBroker, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;

        messageBroker.Subscribe<NetworkPromptSiegeDefenderVictory>(Handle_NetworkPromptSiegeDefenderVictory);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    private void Handle_NetworkPromptSiegeDefenderVictory(MessagePayload<NetworkPromptSiegeDefenderVictory> payload)
    {
        if (ModInformation.IsServer) return;
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            // Only the winning defender whose party the server named transitions; the attacker ignores it.
            if (!objectManager.TryGetId(MobileParty.MainParty?.Party, out var localPartyId)) return;
            if (Array.IndexOf(obj.DefenderPartyIds ?? Array.Empty<string>(), localPartyId) < 0) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            pendingSettlement = settlement;
        });
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer || pendingSettlement == null) return;
        // The mission screen is still up (or popping): re-establishing PlayerEncounter now is unsafe. Wait.
        if (MissionState.Current != null) return;

        // Vanilla shows the battle's prisoner and loot screens before the siege-defeated menu, and nothing else
        // drives the encounter here. The after-battle update plays them out and finishes the encounter at End.
        var encounter = PlayerEncounter.Current;
        if (encounter != null && !encounter._leaveEncounter &&
            MapEventInitializationBarrier.IsBattleResultEncounter(encounter))
        {
            // A capture conversation is still open; updating now would reopen it every tick.
            if (Campaign.Current.ConversationManager.IsConversationInProgress) return;
            PlayerEncounter.Update();
            if (PlayerEncounter.Current != null) return;
        }

        var settlement = pendingSettlement;
        pendingSettlement = null;
        siegeEventInterface.PromptSiegeDefenderVictory(settlement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPromptSiegeDefenderVictory>(Handle_NetworkPromptSiegeDefenderVictory);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        pendingSettlement = null;
    }
}
