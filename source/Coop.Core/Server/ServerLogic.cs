using Common.Logging;
using Common.LogicStates;
using Coop.Core.Server.States;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server;

/// <summary>
/// Top level server-side state machine logic orchestrator
/// </summary>
public interface IServerLogic : ILogic, IServerState
{
    /// <summary>
    /// Server-side state
    /// </summary>
    IServerState State { get; }
    TState SetState<TState>() where TState : IServerState;
}

/// <inheritdoc cref="IServerLogic"/>
public class ServerLogic : IServerLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerLogic>();

    private readonly ServerContext context;
    private readonly IReadOnlyDictionary<Type, Func<IServerState>> stateFactories;

    public IServerState State
    {
        get { return _state; }
        set
        {
            Logger.Debug("Server is changing to {state} State", value.GetType().Name);

            _state?.Dispose();
            _state = value;
        }
    }

    public bool RunningState => _state is not InitialServerState;

    private IServerState _state;

    public ServerLogic(ServerContext context)
    {
        this.context = context;
        stateFactories = CreateStateFactories();
        SetState<InitialServerState>();
    }

    public void Dispose()
    {
        _state?.Dispose();
    }

    public void Start()
    {
        State.Start();
    }

    public void Stop()
    {
        State.Stop();
    }

    public TState SetState<TState>() where TState : IServerState
    {
        TState newState = (TState)stateFactories[typeof(TState)]();
        State = newState;
        return newState;
    }

    // Explicit, container-free construction of each server state from the shared context.
    private IReadOnlyDictionary<Type, Func<IServerState>> CreateStateFactories() =>
        new Dictionary<Type, Func<IServerState>>
        {
            [typeof(InitialServerState)] = () => new InitialServerState(this, context.MessageBroker, context.RegistryManager, context.ModuleValidator, context.ModuleInfoProvider),
            [typeof(ServerRunningState)] = () => new ServerRunningState(this, context.MessageBroker, context.Network, context.GameStateInterface, context.LoadingInterface),
        };
}
