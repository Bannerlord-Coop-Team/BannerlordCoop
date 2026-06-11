using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Roles;

public readonly struct AllPartyRolesOfHeroRemoved : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public AllPartyRolesOfHeroRemoved(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct PartyRoleOfHeroRemoved : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;
    public readonly PartyRole PartyRole;

    public PartyRoleOfHeroRemoved(Hero hero, MobileParty mobileParty, PartyRole partyRole)
    {
        Hero = hero;
        MobileParty = mobileParty;
        PartyRole = partyRole;
    }
}

public readonly struct PartyScoutSet : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public PartyScoutSet(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct PartyQuartermasterSet : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public PartyQuartermasterSet(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct PartyEngineerSet : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public PartyEngineerSet(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}

public readonly struct PartySurgeonSet : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MobileParty;

    public PartySurgeonSet(Hero hero, MobileParty mobileParty)
    {
        Hero = hero;
        MobileParty = mobileParty;
    }
}