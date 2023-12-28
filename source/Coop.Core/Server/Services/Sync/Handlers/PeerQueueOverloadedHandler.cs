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

namespace Coop.Core.Server.Services.Sync.Handlers
{
    internal class PeerQueueOverloadedHandler : IHandler
    {
        private static readonly long POLL_INTERVAL = 100;

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
            poller = new(Poll, TimeSpan.FromMilliseconds(POLL_INTERVAL));

            messageBroker.Subscribe<PeerQueueOverloaded>(Handle);

            poller.Start();
        }

        private void Handle(MessagePayload<PeerQueueOverloaded> payload) 
        {
            if (SafeAdd(payload.What.NetPeer))
            {
                //TODO: set originalSpeed
                originalSpeed = TimeControlEnum.Play_1x;

                //TODO: check for loaders OR replace with helper
                messageBroker.Publish(this, new SetTimeControlMode(originalSpeed));
                network.SendAll(new NetworkTimeSpeedChanged(originalSpeed));

                var msg = new SendInformationMessage($"{overloadedPeers} clients are catching up, pausing");
                messageBroker.Publish(this, msg);
                network.SendAll(msg);
                poller.Start();
            }
        }

        private void Poll(TimeSpan _)
        {
            List<NetPeer> toRemove = new();

            foreach (var peer in new HashSet<NetPeer>(overloadedPeers))
            {
                if (peer.GetPacketsCountInReliableQueue(0, false) == 0)
                {
                    toRemove.Add(peer);
                }
            }

            if (toRemove.Count == 0)
            {
                return;
            }

            lock (overloadedPeers) // To prevent changes, until we notify the the clients
            {
                toRemove.ForEach(peer => { overloadedPeers.Remove(peer); });

                if (overloadedPeers.Count == 0)
                {
                    //TODO: check for loaders OR replace with helper
                    messageBroker.Publish(this, new SetTimeControlMode(originalSpeed));
                    network.SendAll(new NetworkTimeSpeedChanged(originalSpeed));

                    var msg = new SendInformationMessage("All clients synchronized, resuming");
                    messageBroker.Publish(this, msg);
                    network.SendAll(msg);

                    poller.Stop();
                    return;
                }
            }
        }

        private bool SafeAdd(NetPeer peer)
        {
            lock(overloadedPeers)
            {
                return overloadedPeers.Add(peer);
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PeerQueueOverloaded>(Handle);
            poller.Stop();
        }
    }
}
