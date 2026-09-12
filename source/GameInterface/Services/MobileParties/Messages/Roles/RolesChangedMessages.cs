using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Roles;

public readonly struct RemoveAllPartyRolesOfHero : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public RemoveAllPartyRolesOfHero(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct RemovePartyRoleOfHero : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;
    public readonly PartyRole PartyRole;

    public RemovePartyRoleOfHero(Hero hero, MobileParty mobileParty, PartyRole partyRole)
    {
        Hero = hero;
        MobileParty = mobileParty;
        PartyRole = partyRole;
    }
}

public readonly struct RemoveOnePartyRoleOfHero : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public RemoveOnePartyRoleOfHero(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartyScout : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartyScout(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartyQuartermaster : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartyQuartermaster(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartyEngineer : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartyEngineer(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartySurgeon : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartySurgeon(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartyFirstMate : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartyFirstMate(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct SetPartyNavigator : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public SetPartyNavigator(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}