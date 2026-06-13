using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Handlers;

internal class ChangeOwnerOfWorkshopHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeOwnerOfWorkshopHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ChangeOwnerOfWorkshopHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<WorkshopOwnerChanged>(Handle_WorkshopOwnerChanged);
        messageBroker.Subscribe<ChangeWorkshopOwner>(Handle_ChangeWorkshopOwner);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WorkshopOwnerChanged>(Handle_WorkshopOwnerChanged);
        messageBroker.Unsubscribe<ChangeWorkshopOwner>(Handle_ChangeWorkshopOwner);
    }

    private void Handle_WorkshopOwnerChanged(MessagePayload<WorkshopOwnerChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewOwner, out var newOwnerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.WorkshopType, out var workshopTypeId)) return;

        var message = new ChangeWorkshopOwner(workshopId, newOwnerId, workshopTypeId, obj.What.Capital, obj.What.Cost);
        network.SendAll(message);
    }
    
    private void Handle_ChangeWorkshopOwner(MessagePayload<ChangeWorkshopOwner> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewOwnerId, out var newOwner)) return;
        if (!objectManager.TryGetObjectWithLogging<WorkshopType>(obj.What.WorkshopTypeId, out var workshopType)) return;

        ChangeOwnerOfWorkshopActionPatches.ApplyInternalOverride(workshop, newOwner, workshopType, obj.What.Capital, obj.What.Cost);
    }
}
