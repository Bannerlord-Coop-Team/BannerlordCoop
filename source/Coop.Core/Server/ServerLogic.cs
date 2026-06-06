using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections;
using Coop.Core.Server.States;
using Serilog;

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
    private readonly IStateFactory stateFactory;

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

    public ServerLogic(IStateFactory stateFactory)
    {
        this.stateFactory = stateFactory;
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
        TState newState = stateFactory.CreateServerState<TState>(this);
        State = newState;
        return newState;
    }
}
