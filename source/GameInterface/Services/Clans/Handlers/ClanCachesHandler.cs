using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanCachesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanCachesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanCachesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<WarPartyAdded>(Handle_WarPartyAdded);
        messageBroker.Subscribe<AddWarParty>(Handle_AddWarParty);
        messageBroker.Subscribe<WarPartyRemoved>(Handle_WarPartyRemoved);
        messageBroker.Subscribe<RemoveWarParty>(Handle_RemoveWarParty);
        messageBroker.Subscribe<SupporterNotableAdded>(Handle_SupporterNotableAdded);
        messageBroker.Subscribe<AddSupporterNotable>(Handle_AddSupporterNotable);
        messageBroker.Subscribe<SupporterNotableRemoved>(Handle_SupporterNotableRemoved);
        messageBroker.Subscribe<RemoveSupporterNotable>(Handle_RemoveSupporterNotable);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WarPartyAdded>(Handle_WarPartyAdded);
        messageBroker.Unsubscribe<AddWarParty>(Handle_AddWarParty);
        messageBroker.Unsubscribe<WarPartyRemoved>(Handle_WarPartyRemoved);
        messageBroker.Unsubscribe<RemoveWarParty>(Handle_RemoveWarParty);
        messageBroker.Unsubscribe<SupporterNotableAdded>(Handle_SupporterNotableAdded);
        messageBroker.Unsubscribe<AddSupporterNotable>(Handle_AddSupporterNotable);
        messageBroker.Unsubscribe<SupporterNotableRemoved>(Handle_SupporterNotableRemoved);
        messageBroker.Unsubscribe<RemoveSupporterNotable>(Handle_RemoveSupporterNotable);
    }

    private void Handle_WarPartyAdded(MessagePayload<WarPartyAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.WarPartyComponent, out var warPartyComponentId)) return;
        
        // Update changed cache on all clients
        network.SendAll(new AddWarParty(clanId, warPartyComponentId));
    }

    private void Handle_AddWarParty(MessagePayload<AddWarParty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<WarPartyComponent>(obj.What.WarPartyComponentId, out var warPartyComponent)) return;

            clan.OnWarPartyAdded(warPartyComponent);
        });
    }

    private void Handle_WarPartyRemoved(MessagePayload<WarPartyRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.WarPartyComponent, out var warPartyComponentId)) return;

        // Update changed cache on all clients
        network.SendAll(new RemoveWarParty(clanId, warPartyComponentId));
    }

    private void Handle_RemoveWarParty(MessagePayload<RemoveWarParty> obj)
    {
        // Resolve on the game-loop thread, in queue order with deferred component lifecycle applies.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<WarPartyComponent>(obj.What.WarPartyComponentId, out var warPartyComponent)) return;

            clan.OnWarPartyRemoved(warPartyComponent);
        });
    }

    private void Handle_SupporterNotableAdded(MessagePayload<SupporterNotableAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        // Update changed cache on all clients
        network.SendAll(new AddSupporterNotable(clanId, heroId));
    }

    private void Handle_AddSupporterNotable(MessagePayload<AddSupporterNotable> obj)
    {
        // Resolve on the game-loop thread, in queue order with deferred object-creation applies.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            clan.OnSupporterNotableAdded(hero);
        });
    }

    private void Handle_SupporterNotableRemoved(MessagePayload<SupporterNotableRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        // Update changed cache on all clients
        network.SendAll(new RemoveSupporterNotable(clanId, heroId));
    }

    private void Handle_RemoveSupporterNotable(MessagePayload<RemoveSupporterNotable> obj)
    {
        // Resolve on the game-loop thread, in queue order with deferred object-creation applies.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            clan.OnSupporterNotableRemoved(hero);
        });
    }
}
