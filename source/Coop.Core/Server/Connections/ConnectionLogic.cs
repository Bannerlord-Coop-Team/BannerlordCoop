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
    private readonly object stateGate = new object();
    private bool disposed;

    public IConnectionState State
    {
        get
        {
            lock (stateGate)
            {
                return _state;
            }
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

    public bool IsLoading => State?.IsLoading ?? false;

    public TState SetState<TState>() where TState : IConnectionState
    {
        TState newState = (TState)stateFactories[typeof(TState)]();

        lock (stateGate)
        {
            if (disposed)
            {
                // A state constructed concurrently with disconnect must never become current.
                newState.Dispose();
                return newState;
            }

            Logger.Debug("Connection is changing to {state} State", newState.GetType().Name);
            _state?.Dispose();
            _state = newState;
        }

        // Notify after assignment so the registry observes the new state when it recomputes
        // how many connections are loading.
        context.MessageBroker.Publish(this, new ConnectionStateChanged());
        return newState;
    }

    // Explicit, container-free construction of each connection state from the shared context.
    private IReadOnlyDictionary<Type, Func<IConnectionState>> CreateStateFactories() =>
        new Dictionary<Type, Func<IConnectionState>>
        {
            [typeof(ResolveCharacterState)] = () => new ResolveCharacterState(this, context.MessageBroker, context.Network, context.ModuleValidator, context.PlayerManager, context.ObjectManager, context.ModuleInfoProvider, context.ExistingPlayerSender),
            [typeof(CreateCharacterState)] = () => new CreateCharacterState(this, context.ObjectManager, context.MessageBroker, context.Network, context.HeroInterface, context.PlayerManager, context.ExistingPlayerSender),
            [typeof(TransferSaveState)] = () => new TransferSaveState(this, context.Network, context.CoopSessionProvider, context.SaveInterface, context.ConnectionMessageQueue, context.Coalescer, context.AttachmentIdMapper, context.ServerOptionsProvider),
            [typeof(LoadingState)] = () => new LoadingState(this, context.MessageBroker, context.Network, context.JoinCampaignBaselineSender, context.ConnectionMessageQueue, context.Coalescer),
            [typeof(CampaignState)] = () => new CampaignState(this, context.MessageBroker),
            [typeof(MissionState)] = () => new MissionState(this, context.MessageBroker),
        };

    public void Dispose()
    {
        IConnectionState state;

        lock (stateGate)
        {
            if (disposed) return;

            disposed = true;
            state = _state;
            _state = null;
        }

        state?.Dispose();
    }

    public void CreateCharacter() => State.CreateCharacter();

    public void TransferSave() => State.TransferSave();

    public void Load() => State.Load();

    public void EnterCampaign() => State.EnterCampaign();

    public void EnterMission() => State.EnterMission();
}
