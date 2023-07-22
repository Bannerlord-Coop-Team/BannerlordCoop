using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Handlers;

internal class NewHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public NewHeroHandler(
        IHeroInterface heroInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
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
        }
    }

    

    private void Handle(MessagePayload<RegisterNewPlayerHero> obj)
    {
        byte[] bytes = obj.What.Bytes;
        var controllerId = obj.What.ControllerId;
        var sendingPeer = obj.What.SendingPeer;

        try
        {
            Hero hero = null;
            GameLoopRunner.RunOnMainThread(() =>
            {
                hero = heroInterface.UnpackMainHero(controllerId, bytes);
            }, blocking: true);

            heroInterface.HandleNewHero(hero);

            Logger.Debug("New Hero ID: {id}", hero.StringId);

            var registerMessage = new NewPlayerHeroRegistered(sendingPeer, hero);

            messageBroker.Respond(obj.Who, registerMessage);
        }
        catch(Exception e)
        {
            Logger.Error("Error while unpacking new Hero: {error}", e.Message);
        }
    }
}
