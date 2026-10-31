using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.CampaignBehaviors.Messages;
using Coop.Naval.PortScreen;
using GameInterface.Services.Players.Interfaces;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Handlers;

/// <summary>
/// Routes a client's port repair to the server, which repairs the fleet if the player can still afford it.
/// </summary>
internal class RepairShipsAtPortHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPortTransactionPricer portTransactionPricer;
    private readonly IPlayerMessageInterface playerMessageInterface;

    public RepairShipsAtPortHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPortTransactionPricer portTransactionPricer,
        IPlayerMessageInterface playerMessageInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.portTransactionPricer = portTransactionPricer;
        this.playerMessageInterface = playerMessageInterface;

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

            var payer = mainParty.LeaderHero;
            if (payer == null) return;

            // The client's balance can be stale, e.g. a double click, and the repair payment caps at the gold left
            if (portTransactionPricer.GetRepairCost(mainParty.Party) > payer.Gold)
            {
                playerMessageInterface.SendInformationMessageToHero(payer, "The ships were not repaired because you do not have enough gold.");
                network.Send(obj.Who as NetPeer, new NetworkRepairShipsAtPortReply());
                return;
            }

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
