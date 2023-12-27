using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.Sync;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Coop.Core.Client.Network
{
    /// <summary>
    /// Queued packet are being handled on a separate execution thread.
    /// If a packet queue is too large, a message is published to the server
    /// requesting a pause.
    /// </summary>
    internal class PacketQueue
    {
        private readonly IPacketManager manager;
        private readonly INetwork network;
        private readonly Thread runner;

        private ConcurrentQueue<Tuple<NetPeer, IPacket>> queue;

        private bool run;
        private bool isSynchronized;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PacketQueue(IPacketManager manager, INetwork network)
        {
            this.manager = manager;
            this.network = network;

            queue = new();

            run = false;
            isSynchronized = true;
            
            runner = new(Run);
        }

        /// <summary>
        /// Starts the packet handling.
        /// </summary>
        public void Start()
        {
            run = true;
            runner.Start();
        }

        /// <summary>
        /// Stops the packet handling. Blocks until exit.
        /// </summary>
        public void Stop()
        {
            run = false;
            runner.Join();
        }

        /// <summary>
        /// Queues the packet for handling.
        /// </summary>
        /// <param name="peer">Origin</param>
        /// <param name="packet">Packet</param>
        public void Receive(NetPeer peer, IPacket packet)
        {
            queue.Enqueue(Tuple.Create(peer, packet));
        }

        /// <summary>
        /// Discards the queue.
        /// </summary>
        public void Discard()
        {
            queue = new();
        }

        private void Run()
        {
            while(run)
            {
                if (queue.TryDequeue(out Tuple<NetPeer, IPacket> t)) 
                {
                    //TODO: figure out a max queue size amount
                    if (queue.Count > 80 && isSynchronized)
                    {
                        network.SendAll(new NetworkSyncWait());
                        isSynchronized = false;
                    }

                    if (!isSynchronized && queue.Count == 0)
                    {
                        network.SendAll(new NetworkSyncComplete());
                        isSynchronized = true;
                    }

                    var peer = t.Item1;
                    var packet = t.Item2;

                    manager.HandleReceive(peer, packet);
                }
            }
        }
    }
}
