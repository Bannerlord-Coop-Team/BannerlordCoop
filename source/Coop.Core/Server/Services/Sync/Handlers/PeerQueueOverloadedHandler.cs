using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Coop.Core.Server.Services.Sync.Handlers
{
    internal class PeerQueueOverloadedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        private readonly Poller poller;
        private readonly HashSet<NetPeer> overloadedPeers;

        private TimeControlEnum originalSpeed;

        public PeerQueueOverloadedHandler(
            IMessageBroker messageBroker, 
            INetwork network
        )
        {
            this.messageBroker = messageBroker;
            this.network = network;

            overloadedPeers = new();
            poller = new(Poll, TimeSpan.FromMilliseconds(100));

            messageBroker.Subscribe<PeerQueueOverloaded>(Handle);

            poller.Start();
        }

        private void Handle(MessagePayload<PeerQueueOverloaded> payload) 
        {
            Monitor.Enter(overloadedPeers);

            if (overloadedPeers.Add(payload.What.NetPeer))
            {
                Monitor.Exit(overloadedPeers);
                //TODO: set originalSpeed
                originalSpeed = TimeControlEnum.Play_1x;

                //TODO: check for loaders OR replace with helper
                messageBroker.Publish(this, new SetTimeControlMode(originalSpeed));
                network.SendAll(new NetworkTimeSpeedChanged(originalSpeed));

                var msg = new SendInformationMessage($"{overloadedPeers} clients are catching up, pausing");
                messageBroker.Publish(this, msg);
                network.SendAll(msg);
            }
        }

        private void Poll(TimeSpan _)
        {
            HashSet<NetPeer> toRemove = new();

            foreach (var peer in new HashSet<NetPeer>(overloadedPeers))
            {
                if (peer.GetPacketsCountInReliableQueue(0, false) == 0)
                {
                    toRemove.Add(peer);
                }
            }

            //TODO: maybe concurrent hashset?
            Monitor.Enter(overloadedPeers);
            if (toRemove.Count == 0)
            {
                Monitor.Exit(overloadedPeers);
                return;
            }

            foreach (var item in toRemove)
            {
                overloadedPeers.Remove(item);
            }

            if (overloadedPeers.Count == 0)
            {
                Monitor.Exit(overloadedPeers);

                //TODO: check for loaders OR replace with helper
                messageBroker.Publish(this, new SetTimeControlMode(originalSpeed));
                network.SendAll(new NetworkTimeSpeedChanged(originalSpeed));

                var msg = new SendInformationMessage("All clients synchronized, resuming");
                messageBroker.Publish(this, msg);
                network.SendAll(msg);

                return;
            }
            Monitor.Exit(overloadedPeers);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PeerQueueOverloaded>(Handle);
            poller.Stop();
        }
    }
}
