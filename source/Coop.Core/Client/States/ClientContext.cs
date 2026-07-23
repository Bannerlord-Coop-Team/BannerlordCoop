using Common.Messaging;
using Common.Network;
using Coop.Core.Common;
using GameInterface;
using GameInterface.Registry;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// Aggregates the services shared across the client states so each state can be created with
/// <c>new</c> instead of being resolved from the DI container. Threaded through the client state
/// machine by <see cref="ClientLogic"/>.
/// </summary>
public class ClientContext
{
    public ClientContext(
        IMessageBroker messageBroker,
        INetwork network,
        IGameInterface gameInterface,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface,
        IControllerIdProvider controllerIdProvider,
        ICoopFinalizer coopFinalizer,
        IModuleInfoProvider moduleInfoProvider,
        IHeroInterface heroInterface,
        IRegistryManager registryManager,
        IPlayerManager playerManager)
    {
        MessageBroker = messageBroker;
        Network = network;
        GameInterface = gameInterface;
        GameStateInterface = gameStateInterface;
        LoadingInterface = loadingInterface;
        ControllerIdProvider = controllerIdProvider;
        CoopFinalizer = coopFinalizer;
        ModuleInfoProvider = moduleInfoProvider;
        HeroInterface = heroInterface;
        RegistryManager = registryManager;
        PlayerManager = playerManager;
    }

    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IGameInterface GameInterface { get; }
    public IGameStateInterface GameStateInterface { get; }
    public ILoadingInterface LoadingInterface { get; }
    public IControllerIdProvider ControllerIdProvider { get; }
    public ICoopFinalizer CoopFinalizer { get; }
    public IModuleInfoProvider ModuleInfoProvider { get; }
    public IHeroInterface HeroInterface { get; }
    public IRegistryManager RegistryManager { get; }
    public IPlayerManager PlayerManager { get; }
}
