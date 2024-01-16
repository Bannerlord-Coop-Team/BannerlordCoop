using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Entity.Data;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace GameInterface.Services.Heroes.Handlers;

internal class HeroRegistryHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    public HeroRegistryHandler(
        IHeroInterface heroInterface,
        IMessageBroker messageBroker,
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        var previousHero = obj.What.PreviousHero;
        var newHero = obj.What.NewHero;

        if (controlledEntityRegistry.TryGetControlledEntity(previousHero.StringId, out ControlledEntity previousHeroEntity))
        {
            controlledEntityRegistry.RemoveAsControlled(previousHeroEntity);
        }

        controlledEntityRegistry.RegisterAsControlled(controllerIdProvider.ControllerId, newHero.StringId);
    }
}
