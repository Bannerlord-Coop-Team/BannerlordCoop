using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    public readonly struct RegisterNewPlayerHero : ICommand
    {
        // TODO change to peer id
        public Guid RegistrationEventId { get; }

        public byte[] Bytes { get; }
        public RegisterNewPlayerHero(Guid registrationId, byte[] bytes)
        {
            RegistrationEventId = registrationId;
            Bytes = bytes;
        }
    }

    public readonly struct NewPlayerHeroRegistered : IEvent
    {
        public Guid RegistrationEventId { get; }

        public string HeroStringId { get; }
        public string PartyStringId { get; }
        public string CharacterObjectStringId { get; }
        public string ClanStringId { get; }

        public NewPlayerHeroRegistered(Guid registrationId, Hero hero)
        {
            RegistrationEventId = registrationId;
            HeroStringId = hero.StringId;
            PartyStringId = hero.PartyBelongedTo.StringId;
            CharacterObjectStringId = hero.CharacterObject.StringId;
            ClanStringId = hero.Clan.StringId;
        }
    }
}