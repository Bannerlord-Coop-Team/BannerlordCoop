using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using System;

namespace Coop.Core.Client.Services.Heroes.Handlers
{
    /// <summary>
    /// Client handler for LastTimeStampForActivity
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
        }
    }

}
