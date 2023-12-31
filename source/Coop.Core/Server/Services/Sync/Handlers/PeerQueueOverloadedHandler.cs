using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using Serilog;
using System;
namespace Coop.Core.Server.Services.Sync.Handlers
{
    internal class PeerQueueOverloadedHandler : IHandler
    {
        private static readonly long POLL_INTERVAL = 500;

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        private readonly TimeHandler timeHandler;
        private readonly ClientRegistry clientRegistry;

        private readonly ILogger logger;
        private readonly Poller poller;

        private TimeControlEnum originalSpeed;

        private readonly object syncLock = new();

        public PeerQueueOverloadedHandler(
            IMessageBroker messageBroker, 
            INetwork network,
            ClientRegistry clientRegistry,
            TimeHandler timeHandler
        )
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.clientRegistry = clientRegistry;

            this.timeHandler = timeHandler;

            logger = LogManager.GetLogger<PeerQueueOverloaded>();

            poller = new(Poll, TimeSpan.FromMilliseconds(POLL_INTERVAL));

            messageBroker.Subscribe<PeerQueueOverloaded>(Handle);
        }

        private void Handle(MessagePayload<PeerQueueOverloaded> payload)
        {
            lock (syncLock)
            {
                if (clientRegistry.OverloadedPeers.Contains(payload.What.NetPeer))
                    return;

                if (timeHandler.TryGetTimeControlMode(out TimeControlEnum t))
                {
                    originalSpeed = t;
                }
                else
                {
                    originalSpeed = TimeControlEnum.Play_1x;
                }
                if (originalSpeed != TimeControlEnum.Pause)
                {
                    timeHandler.SetTimeMode(TimeControlEnum.Pause);
                }

                clientRegistry.ConnectionStates[payload.What.NetPeer].IsOverloaded = true;

                var msg = new SendInformationMessage($"{clientRegistry.OverloadedPeers.Count} clients are catching up, pausing");
                messageBroker.Publish(this, msg);
                network.SendAll(msg);

                logger.Information("Clients overloaded, paused.");

                if (!poller.IsRunning)
                    poller.Start();
            }
        }

        private void Poll(TimeSpan _)
        {
            lock (syncLock)
            {
                foreach (var connection in clientRegistry.ConnectionStates)
                {
                    if (connection.Value.IsOverloaded && connection.Key.GetPacketsCountInReliableQueue(0, false) == 0)
                    {
                        clientRegistry.ConnectionStates[connection.Key].IsOverloaded = false;
                    }
                }

                if (!clientRegistry.PlayersOverloaded)
                {
                    if (timeHandler.SetTimeMode(originalSpeed))
                    {
                        var msg = new SendInformationMessage("All clients synchronized, resuming");
                        messageBroker.Publish(this, msg);
                        network.SendAll(msg);

                        logger.Information("Clients synchronised, resuming.");

                        poller.Stop();
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PeerQueueOverloaded>(Handle);
            poller.Stop();
        }
    }
}
