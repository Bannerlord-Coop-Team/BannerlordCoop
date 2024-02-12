using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using Coop.Core.Server.Services.Template.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Heroes.Handlers;

/// <summary>
/// TODO describe class
/// </summary>
internal class ServerHeroHandler : IHandler
{
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

        var creationTasks = server.ConnectedPeers.Select(peer =>
        {
            var tcs = new TaskCompletionSource<NetworkHeroCreated>();

            messageBroker.Subscribe<NetworkHeroCreated>(payload =>
            {
                if (payload.Who as NetPeer == peer)
                {
                    tcs.SetResult(payload.What);
                }
            });

            return tcs.Task;
        }).ToArray();

        var message = new NetworkCreateHero(payload.Data);
        server.SendAll(message);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Task.WaitAll(creationTasks, cts.Token);
    }

    private void Handle_HeroNameChanged(MessagePayload<HeroNameChanged> obj)
    {
        var payload = obj.What;

        var message = new NetworkChangeHeroName(payload.Data);

        server.SendAll(message);
    }
}
