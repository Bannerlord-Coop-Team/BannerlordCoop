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
    public IStateFactory StateFactory { get; }
    public Player Player { get; set; }
    private readonly HashSet<Type> RunningStates = new HashSet<Type>
    {
        typeof(MissionState),
        typeof(CampaignState),
    };
    public IClientState State
    {
        get
        {
            if (_state == null)
            {
                // The initial state goes through SetState like any other, so its entry side effects run.
                SetState<MainMenuState>();
            }
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

    public ClientLogic(IStateFactory stateFactory)
    {
        StateFactory = stateFactory;
    }

    public void Start()
    {
        Connect();
    }

    public void Stop()
    {
        Disconnect();
    }

    public TState SetState<TState>() where TState : IClientState
    {
        TState newState = StateFactory.CreateClientState<TState>(this);
        State = newState;

        // Entry side effects run only after the new state is current, so subscribers they trigger never
        // observe the state machine still in the previous state.
        if (newState is ClientStateBase stateBase)
        {
            stateBase.Enter();
        }

        return newState;
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
