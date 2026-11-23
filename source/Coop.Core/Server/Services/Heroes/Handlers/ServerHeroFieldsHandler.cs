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
            messageBroker.Subscribe<HeroStateChanged>(Handle);
            messageBroker.Subscribe<DefaultAgeChanged>(Handle);
            messageBroker.Subscribe<BirthDayChanged>(Handle);
            messageBroker.Subscribe<PowerChanged>(Handle);
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

        private void Handle(MessagePayload<HeroStateChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

            network.SendAll(new NetworkHeroStateChanged(data.HeroState, heroId));
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
            messageBroker.Unsubscribe<HeroStateChanged>(Handle);
            messageBroker.Unsubscribe<DefaultAgeChanged>(Handle);
            messageBroker.Unsubscribe<BirthDayChanged>(Handle);
            messageBroker.Unsubscribe<PowerChanged>(Handle);
            messageBroker.Unsubscribe<HomeSettlementChanged>(Handle);
            messageBroker.Unsubscribe<PregnantChanged>(Handle);
        }
    }
}