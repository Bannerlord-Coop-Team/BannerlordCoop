using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
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
    private readonly IPlayerManager playerManager;

    public ChangeProductionTypeOfWorkshopHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

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

        var message = new ChangeProductionTypeOfWorkshop(workshopId, workshopTypeId);
        network.SendAll(message);
    }
    
    private void Handle_ChangeProductionTypeOfWorkshop(MessagePayload<ChangeProductionTypeOfWorkshop> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
            if (!objectManager.TryGetObjectWithLogging<WorkshopType>(obj.What.WorkshopTypeId, out var workshopType)) return;

            if (!playerManager.TryGetSenderHero(obj.Who, out var senderHero) || workshop.Owner != senderHero)
            {
                Logger.Warning("Ignoring a production change for {WorkshopId} from a peer that does not own it", obj.What.WorkshopId);
                return;
            }

            ChangeProductionTypeOfWorkshopAction.Apply(workshop, workshopType);

            messageBroker.Publish(this, new ClanManagementChanged(workshop.Owner?.Clan, ClanManagementRefresh.Income));
        }, context: nameof(ChangeProductionTypeOfWorkshopHandler));
    }
}
