using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using Serilog;
using System;

namespace GameInterface.Services.Heroes.Handlers;

internal class NewHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IHeroRegistry heroRegistry;
    public NewHeroHandler(
        IHeroInterface heroInterface,
        IMessageBroker messageBroker,
        IHeroRegistry heroRegistry)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;
        this.heroRegistry = heroRegistry;
        messageBroker.Subscribe<PackageMainHero>(Handle);
        messageBroker.Subscribe<RegisterNewPlayerHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PackageMainHero>(Handle);
        messageBroker.Unsubscribe<RegisterNewPlayerHero>(Handle);
    }

    private void Handle(MessagePayload<PackageMainHero> obj)
    {
        try
        {
            byte[] bytes = heroInterface.PackageMainHero();
            messageBroker.Publish(this, new NewHeroPackaged(bytes));
        }
        catch (Exception e)
        {
            Logger.Error("Error while packing new Hero: {error}", e.Message);
            Logger.Error(e.StackTrace);
        }
    }

    private void Handle(MessagePayload<RegisterNewPlayerHero> obj)
    {
        byte[] bytes = obj.What.Bytes;
        var controllerId = obj.What.ControllerId;
        var sendingPeer = obj.What.SendingPeer;

        try
        {
            var playerData = heroInterface.UnpackHero(controllerId, bytes);

            Logger.Debug("New Hero ID: {id}", playerData.HeroStringId);

            var registerMessage = new NewPlayerHeroRegistered(sendingPeer, playerData);

            messageBroker.Respond(obj.Who, registerMessage);
        }
        catch (Exception e)
        {
            Logger.Error("Error while unpacking new Hero: {error}", e);
        }
    }
}