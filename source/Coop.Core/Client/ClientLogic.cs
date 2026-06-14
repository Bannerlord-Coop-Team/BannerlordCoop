using Common.Logging;
using Common.LogicStates;
using Coop.Core.Client.States;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Client;

/// <summary>
/// Top level client-side state machine logic orchestrator
/// </summary>
public interface IClientLogic : ILogic, IClientState
{
    Player Player { get; set; }

    /// <summary>
    /// Client-side state
    /// </summary>
    IClientState State { get; }

    TState SetState<TState>() where TState : IClientState;
}

/// <inheritdoc cref="IClientLogic"/>
public class ClientLogic : IClientLogic
{
    private readonly ILogger Logger = LogManager.GetLogger<ClientLogic>();

    private readonly ClientContext context;
    private readonly IReadOnlyDictionary<Type, Func<IClientState>> stateFactories;

    public Player Player { get; set; }
    private IClientState InitialState => stateFactories[typeof(MainMenuState)]();
    private readonly HashSet<Type> RunningStates = new HashSet<Type>
    {
        typeof(MissionState),
        typeof(CampaignState),
    };
    public IClientState State
    {
        get
        {
            _state ??= InitialState;
            return _state;
        }
        set
        {
            Logger.Debug("Client is changing to {state} State", value.GetType().Name);

            _state?.Dispose();
            _state = value;
        }
    }

    public bool RunningState => RunningStates.Contains(_state.GetType());

    private IClientState _state;

    public ClientLogic(ClientContext context)
    {
        this.context = context;
        stateFactories = CreateStateFactories();
    }

    public TState SetState<TState>() where TState : IClientState
    {
        TState newState = (TState)stateFactories[typeof(TState)]();
        State = newState;
        return newState;
    }

    // Explicit, container-free construction of each client state from the shared context.
    private IReadOnlyDictionary<Type, Func<IClientState>> CreateStateFactories() =>
        new Dictionary<Type, Func<IClientState>>
        {
            [typeof(MainMenuState)] = () => new MainMenuState(this, context.MessageBroker, context.Network, context.GameInterface, context.GameStateInterface, context.LoadingInterface),
            [typeof(ValidateModuleState)] = () => new ValidateModuleState(this, context.MessageBroker, context.Network, context.ControllerIdProvider, context.CoopFinalizer, context.GameStateInterface, context.ModuleInfoProvider),
            [typeof(CharacterCreationState)] = () => new CharacterCreationState(this, context.MessageBroker, context.Network, context.HeroInterface, context.RegistryManager, context.ControllerIdProvider, context.LoadingInterface, context.PlayerManager, context.GameStateInterface, context.CoopFinalizer),
            [typeof(ReceivingSavedDataState)] = () => new ReceivingSavedDataState(this, context.MessageBroker, context.LoadingInterface, context.GameStateInterface),
            [typeof(LoadingState)] = () => new LoadingState(this, context.MessageBroker, context.RegistryManager, context.HeroInterface, context.ControllerIdProvider, context.PlayerManager, context.GameStateInterface, context.LoadingInterface),
            [typeof(CampaignState)] = () => new CampaignState(this, context.MessageBroker, context.Network, context.LoadingInterface, context.GameStateInterface, context.CoopFinalizer),
            [typeof(MissionState)] = () => new MissionState(this, context.MessageBroker, context.GameStateInterface, context.CoopFinalizer),
        };

    public void Start()
    {
        Connect();
    }

    public void Stop()
    {
        Disconnect();
    }

    public void Dispose() => State.Dispose();

    public void Connect() => State.Connect();

    public void Disconnect() => State.Disconnect();

    public void StartCharacterCreation() => State.StartCharacterCreation();

    public void LoadSavedData() => State.LoadSavedData();

    public void ExitGame() => State.ExitGame();

    public void EnterMainMenu() => State.EnterMainMenu();

    public void EnterCampaignState() => State.EnterCampaignState();

    public void EnterMissionState() => State.EnterMissionState();

    public void ValidateModules() => State.ValidateModules();
}
