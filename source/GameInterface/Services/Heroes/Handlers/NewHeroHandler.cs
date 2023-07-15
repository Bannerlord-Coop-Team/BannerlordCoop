using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

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

        try
        {
            Hero hero = heroInterface.UnpackMainHero(controllerId, bytes);

            Logger.Information("New Hero ID: {id}", hero.Id.InternalValue);

            var registerMessage = new NewPlayerHeroRegistered(hero);

            messageBroker.Respond(obj.Who, registerMessage);
        }
        catch(Exception e)
        {
            Logger.Error("Error while unpacking new Hero: {error}", e.Message);
        }
    }
}
