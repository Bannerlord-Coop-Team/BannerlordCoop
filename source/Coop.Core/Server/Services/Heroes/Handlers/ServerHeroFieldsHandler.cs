using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;

namespace Coop.Core.Server.Services.Heroes.Handlers
{
    /// <summary>
    /// Server handler for all fields in the Hero Class
    /// </summary>
    public class ServerHeroFieldsHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;

        public ServerHeroFieldsHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<LastTimeStampChanged>(Handle);
            messageBroker.Subscribe<CharacterObjectChanged>(Handle);
            messageBroker.Subscribe<FirstNameChanged>(Handle);
            messageBroker.Subscribe<NameChanged>(Handle);
            messageBroker.Subscribe<HairTagsChanged>(Handle);
            messageBroker.Subscribe<BeardTagsChanged>(Handle);
            messageBroker.Subscribe<TattooTagsChanged>(Handle);
            messageBroker.Subscribe<HeroStateChanged>(Handle);
            messageBroker.Subscribe<HeroLevelChanged>(Handle);
            messageBroker.Subscribe<SpcDaysInLocationChanged>(Handle);
            messageBroker.Subscribe<DefaultAgeChanged>(Handle);
            messageBroker.Subscribe<BirthDayChanged>(Handle);
            messageBroker.Subscribe<PowerChanged>(Handle);
            messageBroker.Subscribe<CultureChanged>(Handle);
            messageBroker.Subscribe<HomeSettlementChanged>(Handle);
            messageBroker.Subscribe<PregnantChanged>(Handle);
        }

        private void Handle(MessagePayload<PregnantChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkPregnantChanged(data.IsPregnant, heroId));
        }

        private void Handle(MessagePayload<HomeSettlementChanged> payload)
        {
            var data = payload.What;

            string settlementId = null;
            if (data.Settlement != null && !objectManager.TryGetIdWithLogging(data.Settlement, out settlementId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkHomeSettlementChanged(settlementId, heroId));
        }

        private void Handle(MessagePayload<CultureChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Culture, out var cultureId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkCultureChanged(cultureId, heroId));
        }

        private void Handle(MessagePayload<PowerChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkPowerChanged(data.Power, heroId));
        }

        private void Handle(MessagePayload<BirthDayChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkBirthDayChanged(data.BirthDay, heroId));
        }

        private void Handle(MessagePayload<DefaultAgeChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkDefaultAgeChanged(data.Age, heroId));
        }

        private void Handle(MessagePayload<SpcDaysInLocationChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkSpcDaysInLocationChanged(data.Days, heroId));
        }

        private void Handle(MessagePayload<HeroLevelChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkHeroLevelChanged(data.HeroLevel, heroId));
        }

        private void Handle(MessagePayload<HeroStateChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkHeroStateChanged(data.HeroState, heroId));
        }

        private void Handle(MessagePayload<TattooTagsChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkTattooTagsChanged(data.TattooTags, heroId));
        }

        private void Handle(MessagePayload<BeardTagsChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkBeardTagsChanged(data.BeardTags, heroId));
        }

        private void Handle(MessagePayload<HairTagsChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkHairTagsChanged(data.HairTags, heroId));
        }

        private void Handle(MessagePayload<NameChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkNameChanged(data.NewName, heroId));
        }

        private void Handle(MessagePayload<FirstNameChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkFirstNameChanged(data.NewName, heroId));
        }

        private void Handle(MessagePayload<CharacterObjectChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.CharacterObject, out var characterObjectId)) return;
            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkCharacterObjectChanged(characterObjectId, heroId));
        }

        private void Handle(MessagePayload<LastTimeStampChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkLastTimeStampChanged(data.LastTimeStampForActivity, heroId));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LastTimeStampChanged>(Handle);
            messageBroker.Unsubscribe<CharacterObjectChanged>(Handle);
            messageBroker.Unsubscribe<FirstNameChanged>(Handle);
            messageBroker.Unsubscribe<NameChanged>(Handle);
            messageBroker.Unsubscribe<HairTagsChanged>(Handle);
            messageBroker.Unsubscribe<BeardTagsChanged>(Handle);
            messageBroker.Unsubscribe<TattooTagsChanged>(Handle);
            messageBroker.Unsubscribe<HeroStateChanged>(Handle);
            messageBroker.Unsubscribe<HeroLevelChanged>(Handle);
            messageBroker.Unsubscribe<SpcDaysInLocationChanged>(Handle);
            messageBroker.Unsubscribe<DefaultAgeChanged>(Handle);
            messageBroker.Unsubscribe<BirthDayChanged>(Handle);
            messageBroker.Unsubscribe<PowerChanged>(Handle);
            messageBroker.Unsubscribe<CultureChanged>(Handle);
            messageBroker.Unsubscribe<HomeSettlementChanged>(Handle);
            messageBroker.Unsubscribe<PregnantChanged>(Handle);
        }
    }
}