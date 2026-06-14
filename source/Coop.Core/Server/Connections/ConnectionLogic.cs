using Common.Logging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Interface for client connection logic, this has a one-to-one relation per connection
/// </summary>
public interface IConnectionLogic : IConnectionState
{
    NetPeer Peer { get; }
    IConnectionState State { get; }
    TState SetState<TState>() where TState : IConnectionState;
}

/// <inheritdoc cref="IConnectionLogic"/>
public class ConnectionLogic : IConnectionLogic
{
    private readonly ILogger Logger = LogManager.GetLogger<ConnectionLogic>();
    public NetPeer Peer { get; }

    private readonly ConnectionContext context;
    private readonly IReadOnlyDictionary<Type, Func<IConnectionState>> stateFactories;

    public IConnectionState State
    {
        get => _state;
        set
        {
            Logger.Debug("Connection is changing to {state} State", value.GetType().Name);
            _state?.Dispose();
            _state = value;
            // Notify after assignment so the registry observes the new state when it recomputes
            // how many connections are loading.
            context.MessageBroker.Publish(this, new ConnectionStateChanged());
        }
    }

    private IConnectionState _state;

    public ConnectionLogic(NetPeer peer, ConnectionContext context)
    {
        Peer = peer;
        this.context = context;
        stateFactories = CreateStateFactories();
        SetState<ResolveCharacterState>();
    }

    public bool IsLoading => State.IsLoading;

    public TState SetState<TState>() where TState : IConnectionState
    {
        TState newState = (TState)stateFactories[typeof(TState)]();
        State = newState;

        context.MessageBroker.Publish(this, new ConnectionStateChanged());

        return newState;
    }

    // Explicit, container-free construction of each connection state from the shared context.
    private IReadOnlyDictionary<Type, Func<IConnectionState>> CreateStateFactories() =>
        new Dictionary<Type, Func<IConnectionState>>
        {
            [typeof(ResolveCharacterState)] = () => new ResolveCharacterState(this, context.MessageBroker, context.Network, context.ModuleValidator, context.PlayerManager, context.ModuleInfoProvider),
            [typeof(CreateCharacterState)] = () => new CreateCharacterState(this, context.ObjectManager, context.MessageBroker, context.Network, context.HeroInterface, context.PlayerManager, context.GameStateInterface),
            [typeof(TransferSaveState)] = () => new TransferSaveState(this, context.Network, context.CoopSessionProvider, context.SaveInterface, context.TimeControlInterface, context.ConnectionMessageQueue),
            [typeof(LoadingState)] = () => new LoadingState(this, context.MessageBroker),
            [typeof(CampaignState)] = () => new CampaignState(this, context.MessageBroker),
            [typeof(MissionState)] = () => new MissionState(this, context.MessageBroker),
        };

    public void Dispose() => State.Dispose();

    public void CreateCharacter() => State.CreateCharacter();

    public void TransferSave() => State.TransferSave();

    public void Load() => State.Load();

    public void EnterCampaign() => State.EnterCampaign();

    public void EnterMission() => State.EnterMission();
}
