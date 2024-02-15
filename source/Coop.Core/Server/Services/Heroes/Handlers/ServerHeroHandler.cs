using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Heroes.Handlers;

/// <summary>
/// Server side handler for hero related messages.
/// </summary>
internal class ServerHeroHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ServerHeroHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly ICoopServer server;
    private readonly INetworkConfiguration configuration;

    public ServerHeroHandler(IMessageBroker messageBroker, ICoopServer server, INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.server = server;
        this.configuration = configuration;
        messageBroker.Subscribe<HeroCreated>(Handle_HeroCreated);
        messageBroker.Subscribe<HeroNameChanged>(Handle_HeroNameChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroCreated>(Handle_HeroCreated);
        messageBroker.Unsubscribe<HeroNameChanged>(Handle_HeroNameChanged);
    }

    private void Handle_HeroCreated(MessagePayload<HeroCreated> obj)
    {
        var payload = obj.What;

        WaitForAllClientsToCreateHero(payload.Data);
    }

    private void WaitForAllClientsToCreateHero(HeroCreationData heroCreationData)
    {
        // Allow response protocol extra time to account for network latency
        var timeout = configuration.ObjectCreationTimeout + TimeSpan.FromSeconds(1);
        var responseProtocol = new ResponseProtocol<NetworkHeroCreated>(server, messageBroker, timeout);

        var triggerMessage = new NetworkCreateHero(heroCreationData);
        var notifyMessage = new NewHeroSynced();
        responseProtocol.FireAndForget(triggerMessage, notifyMessage);
    }

    class ResponseProtocol<ResponseType> where ResponseType : IMessage
    {
        private readonly ICoopServer server;
        private readonly IMessageBroker messageBroker;
        private readonly Task[] responseTasks;

        public ResponseProtocol(
            ICoopServer server,
            IMessageBroker messageBroker,
            TimeSpan timeout)
        {
            this.server = server;
            this.messageBroker = messageBroker;

            responseTasks = server.ConnectedPeers.Select(peer =>
            {
                var responseTask = new PeerResponse<ResponseType>(messageBroker, peer, timeout);

                return responseTask.Task;
            }).ToArray();
        }

        public void FireAndForget<TriggerType, NotifyType>(TriggerType triggerMessage, NotifyType notifyMessage)
            where TriggerType : IMessage 
            where NotifyType : IMessage
        {
            // Send trigger message -> clients respond with response message -> notify message is sent internally

            // Wait for responses from all clients (cancles after timeout)
            Task.WhenAll(responseTasks).ContinueWith(_ =>
            {
                messageBroker.Publish(this, notifyMessage);
            });

            // Send message to trigger 
            server.SendAll(triggerMessage);
        }
    }

    /// <summary>
    /// Helper class for waiting on a specific peer to respond with a message.
    /// Timeout is supported.
    /// </summary>
    /// <typeparam name="T">Message to wait for</typeparam>
    class PeerResponse<T> : IDisposable where T : IMessage
    {
        private readonly ILogger Logger = LogManager.GetLogger<PeerResponse<T>>();

        public Task Task { get; private set; }

        private readonly IMessageBroker messageBroker;
        private readonly NetPeer peer;
        private readonly TaskCompletionSource<bool> tcs;

        public PeerResponse(IMessageBroker messageBroker, NetPeer peer, TimeSpan timeout)
        {
            this.peer = peer;
            this.messageBroker = messageBroker;
            
            var cts = new CancellationTokenSource(timeout);
            tcs = new TaskCompletionSource<bool>();
            messageBroker.Subscribe<T>(HandleMessage);

            cts.Token.Register(() =>
            {
                if (tcs.TrySetCanceled())
                {
                    Logger.Error("Timeout waiting for peer {peer} to create hero", peer);
                }
            });

            Task = tcs.Task;
            Task.ContinueWith(_ => Dispose());
        }

        private void HandleMessage(MessagePayload<T> payload)
        {
            if (payload.Who as NetPeer == peer)
            {
                tcs.SetResult(true);
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<T>(HandleMessage);
        }
    }

    private void Handle_HeroNameChanged(MessagePayload<HeroNameChanged> obj)
    {
        var payload = obj.What;

        var message = new NetworkChangeHeroName(payload.Data);

        server.SendAll(message);
    }
}
