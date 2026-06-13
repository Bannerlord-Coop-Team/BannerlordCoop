using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Handlers;

internal class ChangeProductionTypeOfWorkshopHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeProductionTypeOfWorkshopHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ChangeProductionTypeOfWorkshopHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ProductionTypeOfWorkshopChanged>(Handle_ProductionTypeOfWorkshopChanged);
        messageBroker.Subscribe<ChangeProductionTypeOfWorkshop>(Handle_ChangeProductionTypeOfWorkshop);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ProductionTypeOfWorkshopChanged>(Handle_ProductionTypeOfWorkshopChanged);
        messageBroker.Unsubscribe<ChangeProductionTypeOfWorkshop>(Handle_ChangeProductionTypeOfWorkshop);
    }

    private void Handle_ProductionTypeOfWorkshopChanged(MessagePayload<ProductionTypeOfWorkshopChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.WorkshopType, out var workshopTypeId)) return;

        var message = new ChangeProductionTypeOfWorkshop(workshopId, workshopTypeId, obj.What.IgnoreCost);
        network.SendAll(message);
    }
    
    private void Handle_ChangeProductionTypeOfWorkshop(MessagePayload<ChangeProductionTypeOfWorkshop> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
        if (!objectManager.TryGetObjectWithLogging<WorkshopType>(obj.What.WorkshopTypeId, out var workshopType)) return;

        ChangeProductionTypeOfWorkshopAction.Apply(workshop, workshopType, obj.What.IgnoreCost);
    }
}
