using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Services.Sync.Messages;
using GameInterface.Services.GameDebug.Commands;
using LiteNetLib;
using System.Collections.Concurrent;
using System.Threading;
using System;

namespace Coop.Core.Client
{
    internal class ClientPacketManager : PacketManagerBase, IPacketManager, IDisposable
    {
        private readonly Thread runner;

        private ConcurrentQueue<Tuple<NetPeer, IPacket>> queue;

        private bool run;
        private bool isSynchronized;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClientPacketManager()
        {
            queue = new();

            isSynchronized = true;

            run = true;
            runner = new(Run);
            runner.Start();
        }

        private void Run()
        {
            while (run)
            {
                if (queue.TryDequeue(out Tuple<NetPeer, IPacket> t))
                {

                    if (!isSynchronized && queue.Count == 0)
                    {
                        MessageBroker.Instance.Publish(this, new SyncChange(true));
                        isSynchronized = true;
                    }

                    //TODO: figure out a max queue size amount
                    if ((queue.Count > 110 && isSynchronized) || GameDebugCommands.ForceSync)
                    {
                        GameDebugCommands.ForceSync = false;
                        MessageBroker.Instance.Publish(this, new SyncChange(false));
                        isSynchronized = false;
                    }

                    var peer = t.Item1;
                    var packet = t.Item2;

                    Process(peer, packet);
                }
            }
        }

        private void Process(NetPeer peer, IPacket packet)
        {
            if (packetHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(peer, packet);
                }
            }
        }

        public override void HandleReceive(NetPeer peer, IPacket packet)
        {
            queue.Enqueue(Tuple.Create(peer, packet));
        }

        public void Dispose()
        {
            run = false;
            runner.Join(10_000);
        }
    }
}
