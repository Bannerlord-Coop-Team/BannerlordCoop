using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using System;

namespace Coop.Core.Server.Services.Heroes.Handlers
{
    /// <summary>
    /// Server handler for all fields in the Hero Class
    /// </summary>
    public class HeroFieldsHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public HeroFieldsHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<LastTimeStampChanged>(Handle);
            messageBroker.Subscribe<CharacterObjectChanged>(Handle);
            messageBroker.Subscribe<FirstNameChanged>(Handle);
        }
        private void Handle(MessagePayload<FirstNameChanged> payload)
        {
            var data = payload.What;
            network.SendAll(new NetworkFirstNameChanged(data.NewName, data.HeroId));
        }
        private void Handle(MessagePayload<CharacterObjectChanged> payload)
        {
            var data = payload.What;
            network.SendAll(new NetworkCharacterObjectChanged(data.CharacterObjectId, data.HeroId));
        }
        private void Handle(MessagePayload<LastTimeStampChanged> payload)
        {
            var data = payload.What;
            network.SendAll(new NetworkLastTimeStampChanged(data.LastTimeStampForActivity, data.HeroId));
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<LastTimeStampChanged>(Handle);
            messageBroker.Unsubscribe<CharacterObjectChanged>(Handle);
        }
    }
}