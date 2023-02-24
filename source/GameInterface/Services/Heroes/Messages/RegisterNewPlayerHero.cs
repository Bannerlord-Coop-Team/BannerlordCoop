using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    public readonly struct RegisterNewPlayerHero : ICommand
    {
        public int PeerId { get; }

        public byte[] Bytes { get; }
        public RegisterNewPlayerHero(int peerId, byte[] bytes)
        {
            PeerId = peerId;
            Bytes = bytes;
        }
    }

    public readonly struct NewPlayerHeroRegistered : IEvent
    {
        public int PeerId { get; }

        public string HeroStringId { get; }
        public string PartyStringId { get; }
        public string CharacterObjectStringId { get; }
        public string ClanStringId { get; }

        public NewPlayerHeroRegistered(int peerId, Hero hero)
        {
            PeerId = peerId;
            HeroStringId = hero.StringId;
            PartyStringId = hero.PartyBelongedTo.StringId;
            CharacterObjectStringId = hero.CharacterObject.StringId;
            ClanStringId = hero.Clan.StringId;
        }
    }
}