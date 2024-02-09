using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Heroes.Handlers;
internal class ClientCreateHeroHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public ClientCreateHeroHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<NetworkCreateHero>(Handle_NetworkCreateHero);
        messageBroker.Subscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreateHero>(Handle_NetworkCreateHero);
    }

    private void Handle_NetworkCreateHero(MessagePayload<NetworkCreateHero> payload)
    {
        var message = new CreateHero(payload.What.Data);
        messageBroker.Publish(this, message);
    }

    private void Handle_NetworkChangeHeroName(MessagePayload<NetworkChangeHeroName> payload)
    {
        var message = new ChangeHeroName(payload.What.Data);
        messageBroker.Publish(this, message);
    }
}
