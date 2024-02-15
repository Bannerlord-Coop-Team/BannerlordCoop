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
        var timeout = configuration.ObjectCreationTimeout;
        var responseProtocol = new ResponseProtocol<NetworkHeroCreated>(server, messageBroker, timeout);

        var triggerMessage = new NetworkCreateHero(heroCreationData);
        var notifyMessage = new NewHeroSynced();
        responseProtocol.FireAndForget(triggerMessage, notifyMessage);
    }

    private void Handle_HeroNameChanged(MessagePayload<HeroNameChanged> obj)
    {
        var payload = obj.What;

        var message = new NetworkChangeHeroName(payload.Data);

        server.SendAll(message);
    }
}
