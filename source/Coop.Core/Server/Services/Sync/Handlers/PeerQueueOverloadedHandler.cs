using Common.Messaging;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Sync.Handlers
{
    internal class PeerQueueOverloadedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly TimeHandler timeHandler;
        private readonly TimeControlEnum originalSpeed;
        private readonly HashSet<NetPeer> overloadedPeers;

        public PeerQueueOverloadedHandler(IMessageBroker messageBroker, TimeHandler timeHandler)
        {
            this.messageBroker = messageBroker;
            this.timeHandler = timeHandler;
            overloadedPeers = new();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        internal void Handle(MessagePayload<PeerQueueOverloaded> payload) 
        {
            if(overloadedPeers.Add(payload.What.NetPeer))
            {
                
            }
        }
    }
}
