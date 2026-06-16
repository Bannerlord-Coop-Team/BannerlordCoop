using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Clans.Messages;

public readonly struct WarPartyAdded : IEvent
{
    public readonly Clan Clan;
    public readonly WarPartyComponent WarPartyComponent;

    public WarPartyAdded(Clan clan, WarPartyComponent warPartyComponent)
    {
        Clan = clan;
        WarPartyComponent = warPartyComponent;
    }
}

public readonly struct WarPartyRemoved : IEvent
{
    public readonly Clan Clan;
    public readonly WarPartyComponent WarPartyComponent;

    public WarPartyRemoved(Clan clan, WarPartyComponent warPartyComponent)
    {
        Clan = clan;
        WarPartyComponent = warPartyComponent;
    }
}

public readonly struct SupporterNotableAdded : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Hero;

    public SupporterNotableAdded(Clan clan, Hero hero)
    {
        Clan = clan;
        Hero = hero;
    }
}

public readonly struct SupporterNotableRemoved : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Hero;

    public SupporterNotableRemoved(Clan clan, Hero hero)
    {
        Clan = clan;
        Hero = hero;
    }
}