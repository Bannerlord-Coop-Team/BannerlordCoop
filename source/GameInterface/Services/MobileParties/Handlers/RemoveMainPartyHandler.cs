using Common.Messaging;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Handlers;

internal class RemoveMainPartyHandler : IHandler
{
    private readonly IMainPartyInterface mainPartyInterface;
    private readonly IMessageBroker messageBroker;

    public RemoveMainPartyHandler(IMainPartyInterface mainPartyInterface, IMessageBroker messageBroker)
    {
        this.mainPartyInterface = mainPartyInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<RemoveMainParty>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RemoveMainParty>(Handle);
    }

    private void Handle(MessagePayload<RemoveMainParty> obj)
    {
        mainPartyInterface.RemoveMainParty();

        messageBroker.Publish(this, new MainPartyRemoved());
    }
}
