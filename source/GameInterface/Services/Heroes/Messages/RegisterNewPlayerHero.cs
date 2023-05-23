using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record RegisterNewPlayerHero : ICommand
{
    public byte[] Bytes { get; }

    public RegisterNewPlayerHero(byte[] bytes)
    {
        Bytes = bytes;
    }
}

public record NewPlayerHeroRegistered : IResponse
{
    public string HeroStringId { get; }
    public string PartyStringId { get; }
    public string CharacterObjectStringId { get; }
    public string ClanStringId { get; }

    public NewPlayerHeroRegistered(Hero hero)
    {
        if (hero == null) return;

        HeroStringId = hero.StringId;
        PartyStringId = hero.PartyBelongedTo.StringId;
        CharacterObjectStringId = hero.CharacterObject.StringId;
        ClanStringId = hero.Clan.StringId;
    }
}