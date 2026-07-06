using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Handlers;

internal class WorkshopDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<WorkshopDataHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public WorkshopDataHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        // Server => Clients
        messageBroker.Subscribe<NewWorkshopDataAdded>(Handle_NewWorkshopDataAdded);
        messageBroker.Subscribe<AddNewWorkshopData>(Handle_AddNewWorkshopData);
        messageBroker.Subscribe<WorkshopDataRemoved>(Handle_WorkshopDataRemoved);
        messageBroker.Subscribe<RemoveWorkshopData>(Handle_RemoveWorkshopData);
        messageBroker.Subscribe<OutputProgressAddedForWarehouse>(Handle_OutputProgressAddedForWarehouse);
        messageBroker.Subscribe<AddOutputProgressForWarehouse>(Handle_AddOutputProgressForWarehouse);
        messageBroker.Subscribe<OutputProgressForTownAdded>(Handle_OutputProgressForTownAdded);
        messageBroker.Subscribe<AddOutputProgressForTown>(Handle_AddOutputProgressForTown);

        // Client => Server => Clients
        messageBroker.Subscribe<IsGettingInputsFromWarehouseSet>(Handle_IsGettingInputsFromWarehouseSet);
        messageBroker.Subscribe<SetIsGettingInputsFromWarehouse>(Handle_SetIsGettingInputsFromWarehouse);
        messageBroker.Subscribe<SetIsGettingInputsFromWarehouseClients>(Handle_SetIsGettingInputsFromWarehouseClients);
        messageBroker.Subscribe<StockProductionInWarehouseRatioSet>(Handle_StockProductionInWarehouseRatioSet);
        messageBroker.Subscribe<SetStockProductionInWarehouseRatio>(Handle_SetStockProductionInWarehouseRatio);
        messageBroker.Subscribe<SetStockProductionInWarehouseRatioClients>(Handle_SetStockProductionInWarehouseRatioClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewWorkshopDataAdded>(Handle_NewWorkshopDataAdded);
        messageBroker.Unsubscribe<AddNewWorkshopData>(Handle_AddNewWorkshopData);
        messageBroker.Unsubscribe<WorkshopDataRemoved>(Handle_WorkshopDataRemoved);
        messageBroker.Unsubscribe<RemoveWorkshopData>(Handle_RemoveWorkshopData);
        messageBroker.Unsubscribe<OutputProgressAddedForWarehouse>(Handle_OutputProgressAddedForWarehouse);
        messageBroker.Unsubscribe<AddOutputProgressForWarehouse>(Handle_AddOutputProgressForWarehouse);
        messageBroker.Unsubscribe<OutputProgressForTownAdded>(Handle_OutputProgressForTownAdded);
        messageBroker.Unsubscribe<AddOutputProgressForTown>(Handle_AddOutputProgressForTown);

        messageBroker.Unsubscribe<IsGettingInputsFromWarehouseSet>(Handle_IsGettingInputsFromWarehouseSet);
        messageBroker.Unsubscribe<SetIsGettingInputsFromWarehouse>(Handle_SetIsGettingInputsFromWarehouse);
        messageBroker.Unsubscribe<SetIsGettingInputsFromWarehouseClients>(Handle_SetIsGettingInputsFromWarehouseClients);
        messageBroker.Unsubscribe<StockProductionInWarehouseRatioSet>(Handle_StockProductionInWarehouseRatioSet);
        messageBroker.Unsubscribe<SetStockProductionInWarehouseRatio>(Handle_SetStockProductionInWarehouseRatio);
        messageBroker.Unsubscribe<SetStockProductionInWarehouseRatioClients>(Handle_SetStockProductionInWarehouseRatioClients);
    }

    private void Handle_NewWorkshopDataAdded(MessagePayload<NewWorkshopDataAdded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            network.SendAll(new AddNewWorkshopData(workshopId));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_AddNewWorkshopData(MessagePayload<AddNewWorkshopData> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                var workshopsBehavior = GetWorkshopsBehavior();
                workshopsBehavior.EnsureBehaviorDataSize();
                if (workshopsBehavior.GetDataOfWorkshop(workshop) == null)
                {
                    workshopsBehavior.AddNewWorkshopData(workshop);
                }
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_WorkshopDataRemoved(MessagePayload<WorkshopDataRemoved> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            network.SendAll(new RemoveWorkshopData(workshopId));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_RemoveWorkshopData(MessagePayload<RemoveWorkshopData> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                GetWorkshopsBehavior().RemoveWorkshopData(workshop);
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_OutputProgressAddedForWarehouse(MessagePayload<OutputProgressAddedForWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            network.SendAll(new AddOutputProgressForWarehouse(workshopId, obj.What.ProgressToAdd));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_AddOutputProgressForWarehouse(MessagePayload<AddOutputProgressForWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                GetWorkshopsBehavior().AddOutputProgressForWarehouse(workshop, obj.What.ProgressToAdd);
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_OutputProgressForTownAdded(MessagePayload<OutputProgressForTownAdded> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            network.SendAll(new AddOutputProgressForTown(workshopId, obj.What.ProgressToAdd));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_AddOutputProgressForTown(MessagePayload<AddOutputProgressForTown> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                GetWorkshopsBehavior().AddOutputProgressForTown(workshop, obj.What.ProgressToAdd);
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_IsGettingInputsFromWarehouseSet(MessagePayload<IsGettingInputsFromWarehouseSet> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            // Send to server
            network.SendAll(new SetIsGettingInputsFromWarehouse(workshopId, obj.What.IsActive));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_SetIsGettingInputsFromWarehouse(MessagePayload<SetIsGettingInputsFromWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            Campaign.Current.GetCampaignBehavior<IWorkshopWarehouseCampaignBehavior>().SetIsGettingInputsFromWarehouse(workshop, obj.What.IsActive);

            network.SendAll(new SetIsGettingInputsFromWarehouseClients(obj.What.WorkshopId, obj.What.IsActive));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_SetIsGettingInputsFromWarehouseClients(MessagePayload<SetIsGettingInputsFromWarehouseClients> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                Campaign.Current.GetCampaignBehavior<IWorkshopWarehouseCampaignBehavior>().SetIsGettingInputsFromWarehouse(workshop, obj.What.IsActive);
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_StockProductionInWarehouseRatioSet(MessagePayload<StockProductionInWarehouseRatioSet> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

            // Send to server
            network.SendAll(new SetStockProductionInWarehouseRatio(workshopId, obj.What.ProgressToAdd));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_SetStockProductionInWarehouseRatio(MessagePayload<SetStockProductionInWarehouseRatio> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            Campaign.Current.GetCampaignBehavior<IWorkshopWarehouseCampaignBehavior>().SetStockProductionInWarehouseRatio(workshop, obj.What.ProgressToAdd);

            network.SendAll(new SetStockProductionInWarehouseRatioClients(obj.What.WorkshopId, obj.What.ProgressToAdd));
        }, context: nameof(WorkshopDataHandler));
    }

    private void Handle_SetStockProductionInWarehouseRatioClients(MessagePayload<SetStockProductionInWarehouseRatioClients> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            using (new AllowedThread())
            {
                Campaign.Current.GetCampaignBehavior<IWorkshopWarehouseCampaignBehavior>().SetStockProductionInWarehouseRatio(workshop, obj.What.ProgressToAdd);
            }
        }, context: nameof(WorkshopDataHandler));
    }

    private WorkshopsCampaignBehavior GetWorkshopsBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
    }
}
