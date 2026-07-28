using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Messages.Roles;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PartyRolesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyRolesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyRolesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<RemoveAllPartyRolesOfHero>(Handle_RemoveAllPartyRolesOfHero);
        messageBroker.Subscribe<NetworkRemoveAllPartyRolesOfHero>(Handle_NetworkRemoveAllPartyRolesOfHero);

        messageBroker.Subscribe<RemovePartyRoleOfHero>(Handle_RemovePartyRoleOfHero);
        messageBroker.Subscribe<NetworkRemovePartyRoleOfHero>(Handle_NetworkRemovePartyRoleOfHero);

        messageBroker.Subscribe<RemoveOnePartyRoleOfHero>(Handle_RemoveOnePartyRoleOfHero);
        messageBroker.Subscribe<NetworkRemoveOnePartyRoleOfHero>(Handle_NetworkRemoveOnePartyRoleOfHero);

        messageBroker.Subscribe<SetPartyScout>(Handle_SetPartyScout);
        messageBroker.Subscribe<NetworkSetPartyScout>(Handle_NetworkSetPartyScout);

        messageBroker.Subscribe<SetPartyQuartermaster>(Handle_SetPartyQuartermaster);
        messageBroker.Subscribe<NetworkSetPartyQuartermaster>(Handle_NetworkSetPartyQuartermaster);

        messageBroker.Subscribe<SetPartyEngineer>(Handle_SetPartyEngineer);
        messageBroker.Subscribe<NetworkSetPartyEngineer>(Handle_NetworkSetPartyEngineer);

        messageBroker.Subscribe<SetPartySurgeon>(Handle_SetPartySurgeon);
        messageBroker.Subscribe<NetworkSetPartySurgeon>(Handle_NetworkSetPartySurgeon);

        messageBroker.Subscribe<SetPartyFirstMate>(Handle_SetPartyFirstMate);
        messageBroker.Subscribe<NetworkSetPartyFirstMate>(Handle_NetworkSetPartyFirstMate);

        messageBroker.Subscribe<SetPartyNavigator>(Handle_SetPartyNavigator);
        messageBroker.Subscribe<NetworkSetPartyNavigator>(Handle_NetworkSetPartyNavigator);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RemoveAllPartyRolesOfHero>(Handle_RemoveAllPartyRolesOfHero);
        messageBroker.Unsubscribe<NetworkRemoveAllPartyRolesOfHero>(Handle_NetworkRemoveAllPartyRolesOfHero);

        messageBroker.Unsubscribe<RemovePartyRoleOfHero>(Handle_RemovePartyRoleOfHero);
        messageBroker.Unsubscribe<NetworkRemovePartyRoleOfHero>(Handle_NetworkRemovePartyRoleOfHero);

        messageBroker.Unsubscribe<RemoveOnePartyRoleOfHero>(Handle_RemoveOnePartyRoleOfHero);
        messageBroker.Unsubscribe<NetworkRemoveOnePartyRoleOfHero>(Handle_NetworkRemoveOnePartyRoleOfHero);

        messageBroker.Unsubscribe<SetPartyScout>(Handle_SetPartyScout);
        messageBroker.Unsubscribe<NetworkSetPartyScout>(Handle_NetworkSetPartyScout);

        messageBroker.Unsubscribe<SetPartyQuartermaster>(Handle_SetPartyQuartermaster);
        messageBroker.Unsubscribe<NetworkSetPartyQuartermaster>(Handle_NetworkSetPartyQuartermaster);

        messageBroker.Unsubscribe<SetPartyEngineer>(Handle_SetPartyEngineer);
        messageBroker.Unsubscribe<NetworkSetPartyEngineer>(Handle_NetworkSetPartyEngineer);

        messageBroker.Unsubscribe<SetPartySurgeon>(Handle_SetPartySurgeon);
        messageBroker.Unsubscribe<NetworkSetPartySurgeon>(Handle_NetworkSetPartySurgeon);

        messageBroker.Unsubscribe<SetPartyFirstMate>(Handle_SetPartyFirstMate);
        messageBroker.Unsubscribe<NetworkSetPartyFirstMate>(Handle_NetworkSetPartyFirstMate);

        messageBroker.Unsubscribe<SetPartyNavigator>(Handle_SetPartyNavigator);
        messageBroker.Unsubscribe<NetworkSetPartyNavigator>(Handle_NetworkSetPartyNavigator);
    }

    private void Handle_RemoveAllPartyRolesOfHero(MessagePayload<RemoveAllPartyRolesOfHero> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkRemoveAllPartyRolesOfHero(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkRemoveAllPartyRolesOfHero(MessagePayload<NetworkRemoveAllPartyRolesOfHero> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.RemoveAllPartyRolesOfHero(hero);
            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_RemovePartyRoleOfHero(MessagePayload<RemovePartyRoleOfHero> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkRemovePartyRoleOfHero(heroId, mobilePartyId, obj.What.PartyRole);
        network.SendAll(message);
    }

    private void Handle_NetworkRemovePartyRoleOfHero(MessagePayload<NetworkRemovePartyRoleOfHero> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.RemovePartyRoleOfHero(hero, data.PartyRole);

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_RemoveOnePartyRoleOfHero(MessagePayload<RemoveOnePartyRoleOfHero> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkRemoveOnePartyRoleOfHero(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkRemoveOnePartyRoleOfHero(MessagePayload<NetworkRemoveOnePartyRoleOfHero> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.RemoveOnePartyRoleOfHero(hero);

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartyScout(MessagePayload<SetPartyScout> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartyScout(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartyScout(MessagePayload<NetworkSetPartyScout> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartyScout(hero);
            mobileParty.ResetCached();

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartyQuartermaster(MessagePayload<SetPartyQuartermaster> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartyQuartermaster(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartyQuartermaster(MessagePayload<NetworkSetPartyQuartermaster> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartyQuartermaster(hero);
            mobileParty.ResetCached(); // Ensure new party size

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartyEngineer(MessagePayload<SetPartyEngineer> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartyEngineer(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartyEngineer(MessagePayload<NetworkSetPartyEngineer> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartyEngineer(hero);
            mobileParty.ResetCached();

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartySurgeon(MessagePayload<SetPartySurgeon> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartySurgeon(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartySurgeon(MessagePayload<NetworkSetPartySurgeon> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartySurgeon(hero);
            mobileParty.ResetCached();

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartyFirstMate(MessagePayload<SetPartyFirstMate> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartyFirstMate(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartyFirstMate(MessagePayload<NetworkSetPartyFirstMate> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartyFirstMate(hero);
            mobileParty.ResetCached();

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private void Handle_SetPartyNavigator(MessagePayload<SetPartyNavigator> obj)
    {
        if (!GetHeroAndPartyIds(obj.What.Hero, obj.What.MobileParty, out var heroId, out var mobilePartyId)) return;

        var message = new NetworkSetPartyNavigator(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkSetPartyNavigator(MessagePayload<NetworkSetPartyNavigator> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!GetHeroAndParty(data.HeroId, data.MobilePartyId, out var hero, out var mobileParty)) return;

            mobileParty.SetPartyNavigator(hero);
            mobileParty.ResetCached();

            UpdateClientVM(obj.Who, data.MobilePartyId);
        });
    }

    private bool GetHeroAndPartyIds(Hero hero, MobileParty mobileParty, out string heroId, out string mobilePartyId)
    {
        heroId = null;
        mobilePartyId = null;
        if (hero != null && !objectManager.TryGetIdWithLogging(hero, out heroId)) return false;

        if (!objectManager.TryGetIdWithLogging(mobileParty, out mobilePartyId)) return false;

        return true;
    }

    private bool GetHeroAndParty(string heroId, string mobilePartyId, out Hero hero, out MobileParty mobileParty)
    {
        hero = null;
        mobileParty = null;
        if (heroId != null && !objectManager.TryGetObjectWithLogging(heroId, out hero)) return false;

        if (!objectManager.TryGetObjectWithLogging(mobilePartyId, out mobileParty)) return false;

        return true;
    }

    private void UpdateClientVM(object who, string mobilePartyId)
    {
        network.Send(who as NetPeer, new RefreshAfterRoleAssignment(mobilePartyId));
    }
}
