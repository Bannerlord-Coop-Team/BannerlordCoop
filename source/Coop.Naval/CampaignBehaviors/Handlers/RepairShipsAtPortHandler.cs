using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.CampaignBehaviors.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Handlers;

internal class RepairShipsAtPortHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public RepairShipsAtPortHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<RequestRepairShipsAtPort>(Handle_RequestRepairShipsAtPort);
        messageBroker.Subscribe<NetworkRequestRepairShipsAtPort>(Handle_NetworkRequestRepairShipsAtPort);
        messageBroker.Subscribe<NetworkRepairShipsAtPortReply>(Handle_NetworkRepairShipsAtPortReply);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestRepairShipsAtPort>(Handle_RequestRepairShipsAtPort);
        messageBroker.Unsubscribe<NetworkRequestRepairShipsAtPort>(Handle_NetworkRequestRepairShipsAtPort);
        messageBroker.Unsubscribe<NetworkRepairShipsAtPortReply>(Handle_NetworkRepairShipsAtPortReply);
    }

    private void Handle_RequestRepairShipsAtPort(MessagePayload<RequestRepairShipsAtPort> obj)
    {
        if (!objectManager.TryGetHandleWithLogging(obj.What.MainParty, out var mainPartyHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(obj.What.CurrentSettlement, out var currentSettlementHandle)) return;

        network.SendAll(new NetworkRequestRepairShipsAtPort(mainPartyHandle, currentSettlementHandle));
    }

    private void Handle_NetworkRequestRepairShipsAtPort(MessagePayload<NetworkRequestRepairShipsAtPort> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyHandle, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementHandle, out var currentSettlement)) return;

            // Reject repair if client no longer has enough gold
            if (mainParty.Ships.Sum(ship => ship.HitPoints < ship.MaxHitPoints ? Campaign.Current.Models.ShipCostModel.GetShipRepairCost(ship, mainParty.Party) : 0f) > mainParty.LeaderHero?.Gold) return;

            foreach (Ship ship in mainParty.Ships)
            {
                if (ship.HitPoints < ship.MaxHitPoints)
                {
                    RepairShipAction.Apply(ship, currentSettlement);
                }
            }

            network.Send(obj.Who as NetPeer, new NetworkRepairShipsAtPortReply());
        });
    }

    private void Handle_NetworkRepairShipsAtPortReply(MessagePayload<NetworkRepairShipsAtPortReply> obj)
    {
        GameThread.RunSafe(() =>
        {
            var menuContext = Campaign.Current?.CurrentMenuContext;
            if (menuContext == null) return;

            Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
        });
    }
}
