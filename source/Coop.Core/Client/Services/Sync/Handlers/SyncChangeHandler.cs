using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Sync.Messages;
using System;

namespace Coop.Core.Client.Services.Sync.Handlers
{
    internal class SyncChangeHandler : IHandler
    {
        private readonly INetwork network;
        private readonly IMessageBroker messageBroker;

        public SyncChangeHandler(INetwork network, IMessageBroker messageBroker)
        {
            this.network = network;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<SyncChange>(Handle);
        }

        public void Handle(MessagePayload<SyncChange> payload)
        {
            network.SendAll(new NetworkSyncStatus(payload.What.Synchronized));
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
