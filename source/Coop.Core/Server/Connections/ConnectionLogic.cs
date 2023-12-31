using Common.Logging;
using Coop.Core.Server.Connections.States;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Interface for client connection logic, this has a one-to-one relation per connection
/// </summary>
public interface IConnectionLogic : IConnectionState
{
    NetPeer Peer { get; }
    IConnectionState State { get; }
    bool IsOverloaded { get; set; }
    TState SetState<TState>() where TState : IConnectionState;
}

/// <inheritdoc cref="IConnectionLogic"/>
public class ConnectionLogic : IConnectionLogic
{
    private readonly ILogger Logger = LogManager.GetLogger<ConnectionLogic>();
    public NetPeer Peer { get; }
    public IStateFactory StateFactory { get; }
    public bool IsOverloaded { get; set; }

    public IConnectionState State 
    {
        get => _state;
        set
        {
            Logger.Debug("Connection is changing to {state} State", value.GetType().Name);
            _state?.Dispose();
            _state = value;
        }
    }        

    private IConnectionState _state;

    public ConnectionLogic(NetPeer playerId, IStateFactory stateFactory)
    {
        Peer = playerId;
        StateFactory = stateFactory;
        SetState<ResolveCharacterState>();
    }

    public TState SetState<TState>() where TState : IConnectionState
    {
        TState newState = StateFactory.CreateConnectionState<TState>(this);
        State = newState;
        return newState;
    }

    public void Dispose() => State.Dispose();

    public void CreateCharacter() => State.CreateCharacter();

    public void TransferSave() => State.TransferSave();

    public void Load() => State.Load();

    public void EnterCampaign() => State.EnterCampaign();

    public void EnterMission() => State.EnterMission();
}
