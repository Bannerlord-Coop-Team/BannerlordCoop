using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    public readonly struct RegisterNewPlayerHero : ICommand
    {
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

        public uint HeroGUID { get; }
        public uint PartyGUID { get; }
        public uint CharacterObjectGUID { get; }
        public uint ClanGUID { get; }

        public NewPlayerHeroRegistered(Guid registrationId, Hero hero)
        {
            RegistrationEventId = registrationId;
            HeroGUID = hero.Id.InternalValue;
            PartyGUID = hero.PartyBelongedTo.Id.InternalValue;
            CharacterObjectGUID = hero.CharacterObject.Id.InternalValue;
            ClanGUID = hero.Clan.Id.InternalValue;
        }
    }
}