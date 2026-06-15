using Common.Messaging;
using Common.Network;
using GameInterface.Registry;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Server.States;

/// <summary>
/// Aggregates the services shared across the server states so each state can be created with
/// <c>new</c> instead of being resolved from the DI container. Threaded through the server state
/// machine by <see cref="ServerLogic"/>.
/// </summary>
public class ServerContext
{
    public ServerContext(
        IMessageBroker messageBroker,
        INetwork network,
        IRegistryManager registryManager,
        IModuleValidator moduleValidator,
        IModuleInfoProvider moduleInfoProvider,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface)
    {
        MessageBroker = messageBroker;
        Network = network;
        RegistryManager = registryManager;
        ModuleValidator = moduleValidator;
        ModuleInfoProvider = moduleInfoProvider;
        GameStateInterface = gameStateInterface;
        LoadingInterface = loadingInterface;
    }

    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IRegistryManager RegistryManager { get; }
    public IModuleValidator ModuleValidator { get; }
    public IModuleInfoProvider ModuleInfoProvider { get; }
    public IGameStateInterface GameStateInterface { get; }
    public ILoadingInterface LoadingInterface { get; }
}
