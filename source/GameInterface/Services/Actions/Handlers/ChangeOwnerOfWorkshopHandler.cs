using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
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
        var peer = obj.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewOwnerId, out var newOwner)) return;
            if (!objectManager.TryGetObjectWithLogging<WorkshopType>(obj.What.WorkshopTypeId, out var workshopType)) return;

            if (workshop.Owner == newOwner)
            {
                SendWorkshopRefresh(peer);
                return;
            }

            if (!objectManager.TryGetIdWithLogging(workshop.Owner, out var currentOwnerId) || currentOwnerId != obj.What.ExpectedOwnerId)
            {
                Logger.Debug(
                    "Rejected workshop owner change because owner changed before apply. WorkshopId={WorkshopId}, ExpectedOwnerId={ExpectedOwnerId}, CurrentOwnerId={CurrentOwnerId}, NewOwnerId={NewOwnerId}",
                    obj.What.WorkshopId,
                    obj.What.ExpectedOwnerId,
                    currentOwnerId,
                    obj.What.NewOwnerId);
                SendWorkshopRefresh(peer);
                return;
            }

            ChangeOwnerOfWorkshopActionPatches.ApplyInternalOverride(workshop, newOwner, workshopType, obj.What.Capital, obj.What.Cost,
                () => SendWorkshopRefresh(peer));
        }, context: nameof(ChangeOwnerOfWorkshopHandler));
    }

    private void SendWorkshopRefresh(NetPeer peer)
    {
        if (peer == null) return;

        // ClanManagementVM when selling a workshop, also used when changing type of workshop
        network.Send(peer, new RefreshWorkshopsList());
    }
}
