using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Services.Heroes.Handlers
{
    /// <summary>
    /// Server handler for LastTimeStampForActivity
    /// </summary>
    public class ServerLastTimeStampHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerLastTimeStampHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<LastTimeStampChanged>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<LastTimeStampChanged>(Handle);
        }
        private void Handle(MessagePayload<LastTimeStampChanged> payload)
        {
            var data = payload.What;
            network.SendAll(new NetworkLastTimeStampChanged(data.LastTimeStampForActivity, data.HeroId));
        }
    }
}