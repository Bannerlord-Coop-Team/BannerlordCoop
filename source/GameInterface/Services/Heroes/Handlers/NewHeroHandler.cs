using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace GameInterface.Services.Heroes.Handlers;


internal class NewHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    public NewHeroHandler(
        IHeroInterface heroInterface,
        IMessageBroker messageBroker)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<RegisterNewPlayerHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterNewPlayerHero>(Handle);
    }

    private void Handle(MessagePayload<RegisterNewPlayerHero> obj)
    {
        byte[] bytes = obj.What.Bytes;
        var controllerId = obj.What.ControllerId;
        var sendingPeer = obj.What.SendingPeer;

        var playerData = heroInterface.UnpackHero(controllerId, bytes);

        Logger.Debug("New Hero ID: {id}", playerData.HeroStringId);

        var registerMessage = new NewPlayerHeroRegistered(sendingPeer, playerData);

        messageBroker.Publish(this, registerMessage);
    }
}