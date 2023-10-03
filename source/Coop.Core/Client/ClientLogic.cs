﻿using Common.Logging;
using Common.LogicStates;
using Coop.Core.Client.States;
using Serilog;

namespace Coop.Core.Client;

/// <summary>
/// Top level client-side state machine logic orchestrator
/// </summary>
public interface IClientLogic : ILogic, IClientState
{
    string ControlledHeroId { get; set; }

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
    public string ControlledHeroId { get; set; }
    public IClientState State 
    {
        get { return _state; }
        set 
        {
            Logger.Debug("Client is changing to {state} State", value.GetType().Name);

            _state?.Dispose();
            _state = value;
        } 
    }

    private IClientState _state;

    public ClientLogic(IStateFactory stateFactory)
    {
        StateFactory = stateFactory;
        SetState<MainMenuState>();
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
