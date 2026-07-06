using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Interfaces;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Workshops.Handlers;

internal class WorkshopWarehouseHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<WorkshopWarehouseHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface;
    private readonly IWorkshopsCampaignBehaviorInterface workshopsCampaignBehaviorInterface;

    public WorkshopWarehouseHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface,
        IWorkshopsCampaignBehaviorInterface workshopsCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionWorkshopPlayerDataInterface = sessionWorkshopPlayerDataInterface;
        this.workshopsCampaignBehaviorInterface = workshopsCampaignBehaviorInterface;
        messageBroker.Subscribe<WorkshopOwnerChanged>(Handle_WorkshopOwnerChanged);
        messageBroker.Subscribe<ChangeWorkshopOwner>(Handle_ChangeWorkshopOwner);
        messageBroker.Subscribe<OutputProducedToWarehouse>(Handle_OutputProducedToWarehouse);
        messageBroker.Subscribe<ProduceOutputToWarehouse>(Handle_ProduceOutputToWarehouse);
        messageBroker.Subscribe<InputConsumedFromWarehouse>(Handle_InputConsumedFromWarehouse);
        messageBroker.Subscribe<ConsumeInputFromWarehouse>(Handle_ConsumeInputFromWarehouse);

        messageBroker.Subscribe<WarehouseRosterManaged>(Handle_WarehouseRosterManaged);
        messageBroker.Subscribe<ManageWarehouseRoster>(Handle_ManageWarehouseRoster);

        messageBroker.Subscribe<TownWorkshopRun>(Handle_TownWorkshopRun);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WorkshopOwnerChanged>(Handle_WorkshopOwnerChanged);
        messageBroker.Unsubscribe<ChangeWorkshopOwner>(Handle_ChangeWorkshopOwner);
        messageBroker.Unsubscribe<OutputProducedToWarehouse>(Handle_OutputProducedToWarehouse);
        messageBroker.Unsubscribe<ProduceOutputToWarehouse>(Handle_ProduceOutputToWarehouse);
        messageBroker.Unsubscribe<InputConsumedFromWarehouse>(Handle_InputConsumedFromWarehouse);
        messageBroker.Unsubscribe<ConsumeInputFromWarehouse>(Handle_ConsumeInputFromWarehouse);

        messageBroker.Unsubscribe<WarehouseRosterManaged>(Handle_WarehouseRosterManaged);
        messageBroker.Unsubscribe<ManageWarehouseRoster>(Handle_ManageWarehouseRoster);

        messageBroker.Unsubscribe<TownWorkshopRun>(Handle_TownWorkshopRun);
    }

    private void Handle_WorkshopOwnerChanged(MessagePayload<WorkshopOwnerChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.OldOwner, out var oldOwnerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Owner, out var ownerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Settlement, out var settlementId)) return;

            WorkshopsCampaignBehavior workshopsBehavior = GetWorkshopsBehavior();
            Workshop workshop = obj.What.Workshop;
            Hero owner = workshop.Owner;
            Hero oldOwner = obj.What.OldOwner;

            if (owner.IsPlayerHero())
            {
                sessionWorkshopPlayerDataInterface.AddNewWarehouseDataIfNeeded(ownerId, settlementId);
                workshopsBehavior.AddNewWorkshopData(workshop);
            }
            else if (oldOwner.IsPlayerHero())
            {
                if (oldOwner.OwnedWorkshops.All((Workshop x) => x.Settlement != workshop.Settlement))
                {
                    if (oldOwner.CurrentSettlement != null && oldOwner.CurrentSettlement == workshop.Settlement)
                    {
                        foreach (ItemRosterElement itemRosterElement in sessionWorkshopPlayerDataInterface.GetWarehouseRoster(oldOwnerId, settlementId))
                        {
                            oldOwner.PartyBelongedTo.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
                        }
                        sessionWorkshopPlayerDataInterface.RemoveWarehouseData(oldOwnerId, settlementId);
                    }
                    sessionWorkshopPlayerDataInterface.RemoveWarehouseData(oldOwnerId, settlementId);
                }
                workshopsBehavior.RemoveWorkshopData(workshop);
            }

            network.SendAll(new ChangeWorkshopOwner(workshopId, oldOwnerId));
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_ChangeWorkshopOwner(MessagePayload<ChangeWorkshopOwner> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OldOwnerId, out var oldOwner)) return;

            WorkshopsCampaignBehavior workshopsBehavior = GetWorkshopsBehavior();
            Hero owner = workshop.Owner;

            // Only update roster for target client. Warehouse data is client specific
            if (owner == Hero.MainHero)
            {
                using (new AllowedThread())
                {
                    workshopsBehavior.EnsureBehaviorDataSize();
                    workshopsBehavior.AddNewWarehouseDataIfNeeded(workshop.Settlement);
                }
                return;
            }
            if (oldOwner == Hero.MainHero && !owner.IsPlayerHero())
            {
                if (oldOwner.OwnedWorkshops.All((Workshop x) => x.Settlement != workshop.Settlement))
                {
                    if (oldOwner.CurrentSettlement != null && oldOwner.CurrentSettlement == workshop.Settlement)
                    {
                        workshopsBehavior.RemoveWarehouseData(oldOwner.CurrentSettlement);
                    }
                    workshopsBehavior.RemoveWarehouseData(workshop.Settlement);
                }
            }
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_OutputProducedToWarehouse(MessagePayload<OutputProducedToWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Owner, out var ownerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Settlement, out var settlementId)) return;

            if (obj.What.Workshop.Owner.IsPlayerHero())
            {
                sessionWorkshopPlayerDataInterface.AddToWarehouse(ownerId, settlementId, obj.What.OutputItem);
            }

            network.SendAll(new ProduceOutputToWarehouse(workshopId, obj.What.OutputItem));
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_ProduceOutputToWarehouse(MessagePayload<ProduceOutputToWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

            // Only update roster for target client. Warehouse data is client specific
            if (workshop.Owner == Hero.MainHero)
            {
                using (new AllowedThread()) // Uses ItemRoster.AddToCounts
                {
                    GetWorkshopsBehavior().ProduceAnOutputToWarehouse(obj.What.OutputItem, workshop);
                }
            }
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_InputConsumedFromWarehouse(MessagePayload<InputConsumedFromWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Owner, out var ownerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop.Settlement, out var settlementId)) return;

            List<ItemRosterElement> warehouseRosterData = sessionWorkshopPlayerDataInterface.GetWarehouseRoster(ownerId, settlementId);
            int num = obj.What.InputCount;
            for (int i = 0; i < warehouseRosterData.Count; i++)
            {
                if (num == 0)
                {
                    return;
                }
                ItemObject itemAtIndex = warehouseRosterData[i].EquipmentElement.Item;
                if (itemAtIndex.ItemCategory == obj.What.ProductionInput)
                {
                    if (!objectManager.TryGetIdWithLogging(itemAtIndex, out var itemId)) continue;

                    int elementNumber = warehouseRosterData[i].Amount;
                    int num2 = MathF.Min(num, elementNumber);
                    num -= num2;

                    // Update on server CoopSession and for specific client who owns the workshop
                    if (obj.What.Workshop.Owner.IsPlayerHero())
                    {
                        sessionWorkshopPlayerDataInterface.RemoveFromWarehouse(ownerId, settlementId, itemAtIndex, obj.What.InputCount);
                        network.SendAll(new ConsumeInputFromWarehouse(workshopId, obj.What.InputCount, itemId));
                    }

                    CampaignEventDispatcher.Instance.OnItemConsumed(itemAtIndex, obj.What.Workshop.Settlement, obj.What.InputCount);
                }
            }
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_ConsumeInputFromWarehouse(MessagePayload<ConsumeInputFromWarehouse> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(obj.What.ItemId, out var item)) return;

            // Only update roster for target client. Warehouse data is client specific
            if (workshop.Owner == Hero.MainHero)
            {
                using (new AllowedThread())
                {
                    GetWorkshopsBehavior().GetWarehouseRoster(workshop.Settlement).AddToCounts(item, -obj.What.InputCount);
                }
            }
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_WarehouseRosterManaged(MessagePayload<WarehouseRosterManaged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;

            sessionWorkshopPlayerDataInterface.UpdateWarehouseRoster(heroId, settlementId, obj.What.NewWarehouseRosterData);

            network.Send(obj.What.NetPeer, new ManageWarehouseRoster(settlementId, obj.What.NewWarehouseRosterData));
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_ManageWarehouseRoster(MessagePayload<ManageWarehouseRoster> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;

            using (new AllowedThread())
            {
                var warehouseRosters = GetWorkshopsBehavior()._warehouseRosterPerSettlement;
                for (int i = 0; i < warehouseRosters.Length; i++)
                {
                    if (warehouseRosters[i].Key == settlement)
                    {
                        warehouseRosters[i].Value.Clear();
                        warehouseRosters[i].Value.Add(obj.What.NewWarehouseRosterData);
                    }
                }
            }
        }, context: nameof(WorkshopWarehouseHandler));
    }

    private void Handle_TownWorkshopRun(MessagePayload<TownWorkshopRun> obj)
    {
        workshopsCampaignBehaviorInterface.RunTownWorkshop(obj.What.Town, obj.What.Workshop);
    }

    private WorkshopsCampaignBehavior GetWorkshopsBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
    }
}
