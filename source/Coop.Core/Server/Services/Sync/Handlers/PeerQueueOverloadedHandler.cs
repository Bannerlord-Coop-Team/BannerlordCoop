using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using LiteNetLib;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Sync.Handlers
{
    internal class PeerQueueOverloadedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        private readonly TimeControlEnum originalSpeed;
        private readonly HashSet<NetPeer> overloadedPeers;

        public PeerQueueOverloadedHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            overloadedPeers = new();
        }

        internal void Handle(MessagePayload<PeerQueueOverloaded> payload) 
        {
            if(overloadedPeers.Add(payload.What.NetPeer))
            {
                
            }
        }


        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
