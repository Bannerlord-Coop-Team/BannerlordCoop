using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Interfaces;
using GameInterface.Services.Workshops.Messages;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops.Handlers;

internal class WarehouseRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<WarehouseRosterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface;

    public WarehouseRosterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionWorkshopPlayerDataInterface = sessionWorkshopPlayerDataInterface;

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
                    foreach (ItemRosterElement itemRosterElement in workshopsBehavior.GetWarehouseRoster(oldOwner.CurrentSettlement))
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
    }

    private void Handle_ChangeWorkshopOwner(MessagePayload<ChangeWorkshopOwner> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OldOwnerId, out var oldOwner)) return;

        WorkshopsCampaignBehavior workshopsBehavior = GetWorkshopsBehavior();
        Hero owner = workshop.Owner;
        if (owner == Hero.MainHero)
        {
            workshopsBehavior.AddNewWarehouseDataIfNeeded(workshop.Settlement);
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
    }

    private WorkshopsCampaignBehavior GetWorkshopsBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
    }
}
