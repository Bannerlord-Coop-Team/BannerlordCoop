using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.GameMenus.Messages;

public readonly struct MenuHeroTakenToParty : IEvent
{
    public readonly Hero Hero;
    public readonly MobileParty MainParty;

    public MenuHeroTakenToParty(Hero hero, MobileParty mainParty)
    {
        Hero = hero;
        MainParty = mainParty;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct MenuTakeHeroToParty : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    public MenuTakeHeroToParty(string heroId, string mainPartyId)
    {
        HeroId = heroId;
        MainPartyId = mainPartyId;
    }
}
