using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using System;

namespace Coop.Core.Client.Services.Heroes.Handlers
{
    /// <summary>
    /// Client handler for Hero fields
    /// </summary>
    public class ClientHeroFieldsHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public ClientHeroFieldsHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<NetworkLastTimeStampChanged>(Handle);
            messageBroker.Subscribe<NetworkCharacterObjectChanged>(Handle);
            messageBroker.Subscribe<NetworkFirstNameChanged>(Handle);
            messageBroker.Subscribe<NetworkNameChanged>(Handle);
            messageBroker.Subscribe<NetworkHairTagsChanged>(Handle);
            messageBroker.Subscribe<NetworkBeardTagsChanged>(Handle);
            messageBroker.Subscribe<NetworkTattooTagsChanged>(Handle);
            messageBroker.Subscribe<NetworkHeroStateChanged>(Handle);
            messageBroker.Subscribe<NetworkHeroLevelChanged>(Handle);
            messageBroker.Subscribe<NetworkSpcDaysInLocationChanged>(Handle);
            messageBroker.Subscribe<NetworkDefaultAgeChanged>(Handle);
            messageBroker.Subscribe<NetworkBirthDayChanged>(Handle);
            messageBroker.Subscribe<NetworkPowerChanged>(Handle);
        }

        private void Handle(MessagePayload<NetworkPowerChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePower(data.Power, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkBirthDayChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeBirthDay(data.BirthDay, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkDefaultAgeChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeDefaultAge(data.Age, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkSpcDaysInLocationChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeSpcDaysInLocation(data.Days, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkHeroLevelChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHeroLevel(data.HeroLevel, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkHeroStateChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHeroState(data.HeroState, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkTattooTagsChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeTattooTags(data.TattooTags, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkBeardTagsChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeBeardTags(data.BeardTags, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkHairTagsChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHairTags(data.HairTags, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkNameChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeName(data.NewName, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkFirstNameChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeFirstName(data.NewName, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkCharacterObjectChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeCharacterObject(data.CharacterObjectId, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkLastTimeStampChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeLastTimeStamp(data.LastTimeStamp, data.HeroId));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkLastTimeStampChanged>(Handle);
            messageBroker.Unsubscribe<NetworkCharacterObjectChanged>(Handle);
            messageBroker.Unsubscribe<NetworkFirstNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHairTagsChanged>(Handle);
            messageBroker.Unsubscribe<NetworkBeardTagsChanged>(Handle);
            messageBroker.Unsubscribe<NetworkTattooTagsChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHeroStateChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHeroLevelChanged>(Handle);
            messageBroker.Unsubscribe<NetworkSpcDaysInLocationChanged>(Handle);
            messageBroker.Unsubscribe<NetworkDefaultAgeChanged>(Handle);
            messageBroker.Unsubscribe<NetworkBirthDayChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPowerChanged>(Handle);
        }
    }
}