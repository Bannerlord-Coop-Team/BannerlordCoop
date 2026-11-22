using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

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
            messageBroker.Subscribe<NetworkHeroStateChanged>(Handle);
            messageBroker.Subscribe<NetworkDefaultAgeChanged>(Handle);
            messageBroker.Subscribe<NetworkBirthDayChanged>(Handle);
            messageBroker.Subscribe<NetworkPowerChanged>(Handle);
            messageBroker.Subscribe<NetworkHomeSettlementChanged>(Handle);
            messageBroker.Subscribe<NetworkPregnantChanged>(Handle);
        }

        private void Handle(MessagePayload<NetworkPregnantChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangePregnant(data.IsPregnant, data.HeroId));
        }

        private void Handle(MessagePayload<NetworkHomeSettlementChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHomeSettlement(data.SettlementStringId, data.HeroId));
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

        private void Handle(MessagePayload<NetworkHeroStateChanged> payload)
        {
            var data = payload.What;
            messageBroker.Publish(this, new ChangeHeroState(data.HeroState, data.HeroId));
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
            messageBroker.Unsubscribe<NetworkHeroStateChanged>(Handle);
            messageBroker.Unsubscribe<NetworkDefaultAgeChanged>(Handle);
            messageBroker.Unsubscribe<NetworkBirthDayChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPowerChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHomeSettlementChanged>(Handle);
            messageBroker.Unsubscribe<NetworkPregnantChanged>(Handle);
        }
    }
}