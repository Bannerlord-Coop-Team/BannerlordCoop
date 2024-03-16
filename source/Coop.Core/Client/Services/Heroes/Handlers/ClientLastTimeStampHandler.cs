using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Heroes.Handlers
{
    /// <summary>
    /// Client handler for LastTimeStampForActivity
    /// </summary>
    public class ClientLastTimeStampHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public ClientLastTimeStampHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<NetworkLastTimeStampChanged>(Handle);
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
