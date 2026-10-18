using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Once the mission pops, plays a winning siege defender's staged loot flow, then opens the siege-defeated menu
/// that the server's replicated teardown skips.
/// </summary>
internal class SiegeDefenderVictoryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

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

            SiegeDefenderFinishPatch.PendingDefenderVictorySettlement = settlement;
        });
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        var settlement = SiegeDefenderFinishPatch.PendingDefenderVictorySettlement;
        if (ModInformation.IsServer || settlement == null) return;
        // The mission screen is still up (or popping): re-establishing PlayerEncounter now is unsafe. Wait.
        if (MissionState.Current != null) return;
        // The encounter menu only resumes the staged encounter while the destroyed event is still attached.
        if (HasStagedBattleResults(PlayerEncounter.Current))
        {
            // A capture conversation is still open; updating now would reopen it every tick.
            if (Campaign.Current.ConversationManager.IsConversationInProgress) return;
            PlayerEncounter.Update();
            if (PlayerEncounter.Current != null) return;
        }

        SiegeDefenderFinishPatch.PendingDefenderVictorySettlement = null;
        siegeEventInterface.PromptSiegeDefenderVictory(settlement);
    }

    private static bool HasStagedBattleResults(PlayerEncounter encounter) =>
        encounter != null && !encounter._leaveEncounter &&
        MapEventInitializationBarrier.IsBattleResultEncounter(encounter);

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPromptSiegeDefenderVictory>(Handle_NetworkPromptSiegeDefenderVictory);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        SiegeDefenderFinishPatch.PendingDefenderVictorySettlement = null;
    }
}
