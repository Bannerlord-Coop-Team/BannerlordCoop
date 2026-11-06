using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Clans.Messages;
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
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.ExpectedOwner, out var expectedOwnerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.NewOwner, out var newOwnerId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.WorkshopType, out var workshopTypeId)) return;

            var message = new ChangeWorkshopOwner(workshopId, expectedOwnerId, newOwnerId, workshopTypeId, obj.What.Capital, obj.What.Cost);
            network.SendAll(message);
        }, context: nameof(ChangeOwnerOfWorkshopHandler));
    }

    private void Handle_ChangeWorkshopOwner(MessagePayload<ChangeWorkshopOwner> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(data.WorkshopId, out var workshop)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewOwnerId, out var newOwner)) return;
            if (!objectManager.TryGetObjectWithLogging<WorkshopType>(data.WorkshopTypeId, out var workshopType)) return;

            var currentOwner = workshop.Owner;
            if (currentOwner == newOwner)
            {
                SendWorkshopRefresh(workshop.Owner);
                return;
            }

            if (!objectManager.TryGetIdWithLogging(currentOwner, out var currentOwnerId) || currentOwnerId != data.ExpectedOwnerId)
            {
                Logger.Debug(
                    "Rejected workshop owner change because owner changed before apply. WorkshopId={WorkshopId}, ExpectedOwnerId={ExpectedOwnerId}, CurrentOwnerId={CurrentOwnerId}, NewOwnerId={NewOwnerId}",
                    data.WorkshopId,
                    data.ExpectedOwnerId,
                    currentOwnerId,
                    data.NewOwnerId);
                SendWorkshopRefresh(currentOwner);
                SendWorkshopRefresh(newOwner);
                return;
            }

            ChangeOwnerOfWorkshopActionPatches.ApplyInternalOverride(workshop, newOwner, workshopType, data.Capital, data.Cost);
        }, context: nameof(ChangeOwnerOfWorkshopHandler));
    }

    private void SendWorkshopRefresh(Hero owner)
    {
        if (owner.Clan == null) return;
        messageBroker.Publish(this, new ClanManagementChanged(owner.Clan, ClanManagementRefresh.Income));
    }
}
