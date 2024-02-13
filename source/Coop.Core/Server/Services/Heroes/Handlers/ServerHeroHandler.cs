using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using Coop.Core.Server.Services.Template.Messages;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Coop.Core.Server.Services.Heroes.Handlers;

/// <summary>
/// Server side handler for hero related messages.
/// </summary>
internal class ServerHeroHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ServerHeroHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly ICoopServer server;

    public ServerHeroHandler(IMessageBroker messageBroker, ICoopServer server)
    {
        this.messageBroker = messageBroker;
        this.server = server;

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
        var targetFunctions = new List<Action<MessagePayload<NetworkHeroCreated>>>();
        var waitingPeers = new List<WaitingPeer<NetworkHeroCreated>>();
        foreach (var peer in server.ConnectedPeers)
        {
            waitingPeers.Add(new WaitingPeer<NetworkHeroCreated>(messageBroker, peer, TimeSpan.FromSeconds(200)));
        }

        var message = new NetworkCreateHero(heroCreationData);
        server.SendAll(message);

        // Waits for all clients to create a hero
        Task.WaitAll(waitingPeers.Select(waiter => waiter.Task).ToArray());

        messageBroker.Publish(this, new NewHeroSynced());
    }

    /// <summary>
    /// Helper class for waiting on a specific peer to respond with a message.
    /// Timeout is supported.
    /// </summary>
    /// <typeparam name="T">Message to wait for</typeparam>
    class WaitingPeer<T> : IDisposable where T : IMessage
    {
        private readonly ILogger Logger = LogManager.GetLogger<ServerHeroHandler>();

        
        public Task Task { get; private set; }

        private readonly IMessageBroker messageBroker;
        private readonly Action<MessagePayload<T>> targetMethod;
        private readonly NetPeer peer;
        private readonly TaskCompletionSource<bool> tcs;

        public WaitingPeer(IMessageBroker messageBroker, NetPeer peer, TimeSpan timeout)
        {
            this.peer = peer;
            this.messageBroker = messageBroker;
            
            var cts = new CancellationTokenSource(timeout);
            tcs = new TaskCompletionSource<bool>();
            messageBroker.Subscribe<T>(HandleMessage);

            cts.Token.Register(() =>
            {
                Logger.Error("Timeout waiting for peer {peer} to create hero", peer);
                tcs.SetCanceled();

                Dispose();
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
