using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;

namespace Coop.Core.Server.Services.Villages.Handlers;

/// <summary>
/// Handles village state changes on the server.
/// </summary>
internal class ServerVillageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ServerVillageHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        // This handles an internal message
        messageBroker.Subscribe<VillageStateChanged>(HandleVillageState);
        messageBroker.Subscribe<VillageTradeBoundChanged>(HandleTradeBound);
        messageBroker.Subscribe<VillageHearthChanged>(HandleHearth);
        messageBroker.Subscribe<VillageTaxAccumulateChanged>(HandleTradeTaxAccumulated);
        messageBroker.Subscribe<VillageDemandTimeChanged>(HandleLastDemandSatisfiedTime);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageStateChanged>(HandleVillageState);
        messageBroker.Unsubscribe<VillageTradeBoundChanged>(HandleTradeBound);
        messageBroker.Unsubscribe<VillageHearthChanged>(HandleHearth);
        messageBroker.Unsubscribe<VillageTaxAccumulateChanged>(HandleTradeTaxAccumulated);
        messageBroker.Unsubscribe<VillageDemandTimeChanged>(HandleLastDemandSatisfiedTime);
    }

    private void HandleLastDemandSatisfiedTime(MessagePayload<VillageDemandTimeChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Village, out var villageId)) return;

        var networkMessage = new NetworkChangeVillageDemandTime(
            villageId,
            obj.LastDemandSatisfiedTime);

        network.SendAll(networkMessage);
    }

    private void HandleTradeTaxAccumulated(MessagePayload<VillageTaxAccumulateChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Village, out var villageId)) return;

        var networkMessage = new NetworkChangeVillageTradeTaxAccumulated(
            villageId,
            obj.TradeTaxAccumulated);

        network.SendAll(networkMessage);
    }

    private void HandleHearth(MessagePayload<VillageHearthChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Village, out var villageId)) return;

        var networkMessage = new NetworkChangeVillageHearth(
            villageId,
            obj.Hearth);

        network.SendAll(networkMessage);
    }

    private void HandleTradeBound(MessagePayload<VillageTradeBoundChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Village, out var villageId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.TradeBound, out var tradeBoundId)) return;

        var networkMessage = new NetworkChangeVillageTradeBound(
            villageId,
            tradeBoundId);

        network.SendAll(networkMessage);
    }

    private void HandleVillageState(MessagePayload<VillageStateChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Village, out var villageId)) return;

        var networkMessage = new NetworkChangeVillageState(
            villageId,
            obj.State);

        network.SendAll(networkMessage);
    }
}