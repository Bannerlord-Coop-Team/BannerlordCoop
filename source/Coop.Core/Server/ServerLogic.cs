using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
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
    IServerState State { get; set; }

    /// <summary>
    /// Networking Server for Server-side
    /// </summary>
    INetwork Network { get; }
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
    private IServerState _state;

    public INetwork Network { get; }

    

    public ServerLogic(INetwork network, IStateFactory stateFactory)
    {
        Network = network;
        this.stateFactory = stateFactory;
    }

    public void Dispose() => State.Dispose();

    public void Start()
    {
        State = stateFactory.CreateServerState<InitialServerState>();
        State.Start();
    }

    public void Stop()
    {
        State.Stop();
        Network.Stop();
    }
}
