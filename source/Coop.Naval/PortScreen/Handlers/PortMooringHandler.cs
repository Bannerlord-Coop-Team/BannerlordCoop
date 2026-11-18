using Common;
using Common.Messaging;
using Coop.Naval.PortScreen.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.PortScreen.Handlers;

internal class PortMooringHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public PortMooringHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkShipMooringChanged>(Handle_NetworkShipMooringChanged);
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
}
