using Common;
using Common.Messaging;
using Coop.Naval.PortScreen.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.PortScreen.Handlers;

/// <summary>
/// [Client] Keeps this client's port stashes to its own clan's moored ships.
/// </summary>
internal class PortMooringHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    // Set on join until the stashes are filtered
    private NavalPlayerData joinedNavalPlayerData;

    public PortMooringHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
        messageBroker.Subscribe<InitializeClientMooredShips>(Handle_InitializeClientMooredShips);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
        messageBroker.Unsubscribe<InitializeClientMooredShips>(Handle_InitializeClientMooredShips);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    private void Handle_NetworkShipMooringChanged(MessagePayload<NetworkShipMooringChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipId, out var ship)) return;

            if (!data.IsMoored)
            {
                settlement.ShipStash.Remove(ship);
                return;
            }

            if (!settlement.ShipStash.Contains(ship)) settlement.ShipStash.Add(ship);
        });
    }

    private void Handle_InitializeClientMooredShips(MessagePayload<InitializeClientMooredShips> obj)
    {
        joinedNavalPlayerData = obj.What.NavalPlayerData ?? new NavalPlayerData(null, null, null, null);
    }

    // The joined save's stashes are filtered once, on the hero change that follows the join, because the clan is only known then
    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        if (joinedNavalPlayerData == null) return;

        var navalPlayerData = joinedNavalPlayerData;
        joinedNavalPlayerData = null;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero.Clan, out var playerClanId)) return;

        KeepOnlyClanMooredShips(navalPlayerData, playerClanId);
    }

    private void KeepOnlyClanMooredShips(NavalPlayerData navalPlayerData, string playerClanId)
    {
        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            var stash = settlement.ShipStash;
            if (stash == null || stash.Count == 0) continue;
            if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) continue;

            for (int i = stash.Count - 1; i >= 0; i--)
            {
                if (navalPlayerData.GetMooredShipOwnerClan(settlementId, i) != playerClanId) stash.RemoveAt(i);
            }
        }
    }
}
