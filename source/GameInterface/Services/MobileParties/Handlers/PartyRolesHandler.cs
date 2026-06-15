using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Roles;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PartyRolesHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<PartyRolesHandler>();

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

        messageBroker.Subscribe<AllPartyRolesOfHeroRemoved>(Handle_AllPartyRolesOfHeroRemoved);
        messageBroker.Subscribe<RemoveAllPartyRolesOfHero>(Handle_RemoveAllPartyRolesOfHero);
        messageBroker.Subscribe<PartyRoleOfHeroRemoved>(Handle_PartyRoleOfHeroRemoved);
        messageBroker.Subscribe<RemovePartyRoleOfHero>(Handle_RemovePartyRoleOfHero);
        messageBroker.Subscribe<PartyScoutSet>(Handle_PartyScoutSet);
        messageBroker.Subscribe<SetPartyScout>(Handle_SetPartyScout);
        messageBroker.Subscribe<PartyQuartermasterSet>(Handle_PartyQuartermasterSet);
        messageBroker.Subscribe<SetPartyQuartermaster>(Handle_SetPartyQuartermaster);
        messageBroker.Subscribe<PartyEngineerSet>(Handle_PartyEngineerSet);
        messageBroker.Subscribe<SetPartyEngineer>(Handle_SetPartyEngineer);
        messageBroker.Subscribe<PartySurgeonSet>(Handle_PartySurgeonSet);
        messageBroker.Subscribe<SetPartySurgeon>(Handle_SetPartySurgeon);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllPartyRolesOfHeroRemoved>(Handle_AllPartyRolesOfHeroRemoved);
        messageBroker.Unsubscribe<RemoveAllPartyRolesOfHero>(Handle_RemoveAllPartyRolesOfHero);
        messageBroker.Unsubscribe<PartyRoleOfHeroRemoved>(Handle_PartyRoleOfHeroRemoved);
        messageBroker.Unsubscribe<RemovePartyRoleOfHero>(Handle_RemovePartyRoleOfHero);
        messageBroker.Unsubscribe<PartyScoutSet>(Handle_PartyScoutSet);
        messageBroker.Unsubscribe<SetPartyScout>(Handle_SetPartyScout);
        messageBroker.Unsubscribe<PartyQuartermasterSet>(Handle_PartyQuartermasterSet);
        messageBroker.Unsubscribe<SetPartyQuartermaster>(Handle_SetPartyQuartermaster);
        messageBroker.Unsubscribe<PartyEngineerSet>(Handle_PartyEngineerSet);
        messageBroker.Unsubscribe<SetPartyEngineer>(Handle_SetPartyEngineer);
        messageBroker.Unsubscribe<PartySurgeonSet>(Handle_PartySurgeonSet);
        messageBroker.Unsubscribe<SetPartySurgeon>(Handle_SetPartySurgeon);
    }

    private void Handle_AllPartyRolesOfHeroRemoved(MessagePayload<AllPartyRolesOfHeroRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new RemoveAllPartyRolesOfHero(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_RemoveAllPartyRolesOfHero(MessagePayload<RemoveAllPartyRolesOfHero> obj)
    {
        var data = obj.What;

        // Removing party roles runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.RemoveAllPartyRolesOfHero(hero);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(RemoveAllPartyRolesOfHero));
            }
        });
    }

    private void Handle_PartyRoleOfHeroRemoved(MessagePayload<PartyRoleOfHeroRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new RemovePartyRoleOfHero(heroId, mobilePartyId, obj.What.PartyRole);
        network.SendAll(message);
    }

    private void Handle_RemovePartyRoleOfHero(MessagePayload<RemovePartyRoleOfHero> obj)
    {
        var data = obj.What;

        // Removing a party role runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.RemovePartyRoleOfHero(hero, data.PartyRole);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(RemovePartyRoleOfHero));
            }
        });
    }

    private void Handle_PartyScoutSet(MessagePayload<PartyScoutSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetPartyScout(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_SetPartyScout(MessagePayload<SetPartyScout> obj)
    {
        var data = obj.What;

        // Setting a party role runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.SetPartyScout(hero);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(SetPartyScout));
            }
        });
    }

    private void Handle_PartyQuartermasterSet(MessagePayload<PartyQuartermasterSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetPartyQuartermaster(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_SetPartyQuartermaster(MessagePayload<SetPartyQuartermaster> obj)
    {
        var data = obj.What;

        // Setting a party role runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.SetPartyQuartermaster(hero);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(SetPartyQuartermaster));
            }
        });
    }

    private void Handle_PartyEngineerSet(MessagePayload<PartyEngineerSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetPartyEngineer(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_SetPartyEngineer(MessagePayload<SetPartyEngineer> obj)
    {
        var data = obj.What;

        // Setting a party role runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.SetPartyEngineer(hero);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(SetPartyEngineer));
            }
        });
    }

    private void Handle_PartySurgeonSet(MessagePayload<PartySurgeonSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetPartySurgeon(heroId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_SetPartySurgeon(MessagePayload<SetPartySurgeon> obj)
    {
        var data = obj.What;

        // Setting a party role runs vanilla campaign code that mutates party-member
        // state the main-thread tick also touches; it must run on the game loop, not
        // the network thread that delivered the command.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

                using (new AllowedThread())
                {
                    mobileParty.SetPartySurgeon(hero);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(SetPartySurgeon));
            }
        });
    }
}
