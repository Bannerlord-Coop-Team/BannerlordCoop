using Common.Messaging;
using LiteNetLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record RegisterNewPlayerHero : ICommand
{
    public NetPeer SendingPeer { get; }
    public string ControllerId { get; }
    public byte[] Bytes { get; }

    public RegisterNewPlayerHero(NetPeer sendingPeer, string controllerId, byte[] bytes)
    {
        SendingPeer = sendingPeer;
        ControllerId = controllerId;
        Bytes = bytes;
    }
}

public record NewPlayerHeroRegistered : IResponse
{
    public NetPeer SendingPeer { get; }
    public string HeroStringId { get; }
    public string PartyStringId { get; }
    public string CharacterObjectStringId { get; }
    public string ClanStringId { get; }

    public NewPlayerHeroRegistered(NetPeer sendingPeer, Hero hero)
    {
        SendingPeer = sendingPeer;

        if (hero == null) return;

        HeroStringId = hero.StringId;
        PartyStringId = hero.PartyBelongedTo.StringId;
        CharacterObjectStringId = hero.CharacterObject.StringId;
        ClanStringId = hero.Clan.StringId;
    }
}