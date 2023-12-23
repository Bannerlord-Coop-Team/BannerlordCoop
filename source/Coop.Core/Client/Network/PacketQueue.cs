using Common.PacketHandlers;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Coop.Core.Client.Network
{
    internal class PacketQueue
    {
        private readonly IPacketManager manager;
        private readonly Thread runner;

        private ConcurrentQueue<Tuple<NetPeer, IPacket>> queue;

        private bool run;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PacketQueue(IPacketManager manager)
        {
            this.manager = manager;
            queue = new();
            
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
                    if (queue.Count > 40)
                    {
                        //TODO: notify
                    }

                    var peer = t.Item1;
                    var packet = t.Item2;

                    manager.HandleReceive(peer, packet);
                }
            }
        }
    }
}
