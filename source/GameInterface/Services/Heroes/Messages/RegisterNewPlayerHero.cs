using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct RegisterNewPlayerHero : ICommand
{
    public Guid TransactionID { get; }

    public byte[] Bytes { get; }

    public RegisterNewPlayerHero(Guid transactionId, byte[] bytes)
    {
        TransactionID = transactionId;
        Bytes = bytes;
    }
}

public readonly struct NewPlayerHeroRegistered : IResponse
{
    public Guid TransactionID { get; }
    public string HeroStringId { get; }
    public string PartyStringId { get; }
    public string CharacterObjectStringId { get; }
    public string ClanStringId { get; }

    public NewPlayerHeroRegistered(Guid transactionID, Hero hero)
    {
        TransactionID = transactionID;
        HeroStringId = hero.StringId;
        PartyStringId = hero.PartyBelongedTo.StringId;
        CharacterObjectStringId = hero.CharacterObject.StringId;
        ClanStringId = hero.Clan.StringId;
    }
}