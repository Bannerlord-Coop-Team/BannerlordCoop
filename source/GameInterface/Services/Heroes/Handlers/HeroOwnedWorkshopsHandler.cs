using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Handlers;

internal class HeroOwnedWorkshopsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroOwnedWorkshopsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public HeroOwnedWorkshopsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<OwnedWorkshopAdded>(Handle_OwnedWorkshopAdded);
        messageBroker.Subscribe<AddOwnedWorkshop>(Handle_AddOwnedWorkshop);
        messageBroker.Subscribe<OwnedWorkshopRemoved>(Handle_OwnedWorkshopRemoved);
        messageBroker.Subscribe<RemoveOwnedWorkshop>(Handle_RemoveOwnedWorkshop);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<OwnedWorkshopAdded>(Handle_OwnedWorkshopAdded);
        messageBroker.Unsubscribe<AddOwnedWorkshop>(Handle_AddOwnedWorkshop);
        messageBroker.Unsubscribe<OwnedWorkshopRemoved>(Handle_OwnedWorkshopRemoved);
        messageBroker.Unsubscribe<RemoveOwnedWorkshop>(Handle_RemoveOwnedWorkshop);
    }

    private void Handle_OwnedWorkshopAdded(MessagePayload<OwnedWorkshopAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

        var message = new AddOwnedWorkshop(heroId, workshopId);
        network.SendAll(message);
    }

    private void Handle_AddOwnedWorkshop(MessagePayload<AddOwnedWorkshop> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;
        
        using (new AllowedThread())
        {
            hero.AddOwnedWorkshop(workshop);
        }
    }

    private void Handle_OwnedWorkshopRemoved(MessagePayload<OwnedWorkshopRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Workshop, out var workshopId)) return;

        var message = new RemoveOwnedWorkshop(heroId, workshopId);
        network.SendAll(message);
    }

    private void Handle_RemoveOwnedWorkshop(MessagePayload<RemoveOwnedWorkshop> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
        if (!objectManager.TryGetObjectWithLogging<Workshop>(obj.What.WorkshopId, out var workshop)) return;

        using (new AllowedThread())
        {
            hero.RemoveOwnedWorkshop(workshop);
        }
    }
}
