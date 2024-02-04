using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Services.Heroes.Handlers;

/// <summary>
/// TODO describe class
/// </summary>
internal class ServerHeroHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerHeroHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<HeroCreated>(Handle_HeroCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroCreated>(Handle_HeroCreated);
    }

    private void Handle_HeroCreated(MessagePayload<HeroCreated> obj)
    {
        var payload = obj.What;

        var message = new NetworkCreateHero(payload.Data);

        network.SendAll(message);
    }
}
