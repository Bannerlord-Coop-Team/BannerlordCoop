using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Manages client connections while one or more connections is receiving the game state
/// through a save transfer. Exposes the live connection states for consumers that need to
/// reason about who is loading.
/// </summary>
public interface IConnectionCollection : IEnumerable<IConnectionState>, IDisposable
{
    IEnumerable<IConnectionLogic> LoadingPeers { get; }
}

/// <inheritdoc cref="IConnectionCollection"/>
public class ConnectionCollection : IConnectionCollection
{
    public ConcurrentDictionary<NetPeer, IConnectionLogic> ConnectionStates { get; private set; } = new();

    public IEnumerable<IConnectionLogic> LoadingPeers => ConnectionStates
        .Select(conn => conn.Value)
        .Where(conn => conn.IsLoading);

    private readonly IMessageBroker messageBroker;
    private readonly ConnectionContext connectionContext;

    private int lastBroadcastLoadingCount;

    public ConnectionCollection(
        IMessageBroker messageBroker,
        ConnectionContext connectionContext)
    {
        this.messageBroker = messageBroker;
        this.connectionContext = connectionContext;
        this.messageBroker.Subscribe<PlayerConnected>(PlayerJoiningHandler);
        this.messageBroker.Subscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
        this.messageBroker.Subscribe<ConnectionStateChanged>(ConnectionStateChangedHandler);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnected>(PlayerJoiningHandler);
        messageBroker.Unsubscribe<PlayerDisconnected>(PlayerDisconnectedHandler);
        messageBroker.Unsubscribe<ConnectionStateChanged>(ConnectionStateChangedHandler);
    }

    internal void PlayerJoiningHandler(MessagePayload<PlayerConnected> obj)
    {
        var playerPeer = obj.What.PlayerPeer;
        ConnectionStates.TryAdd(playerPeer, new ConnectionLogic(playerPeer, connectionContext));
        BroadcastLoadingStateIfChanged();
    }

    internal void PlayerDisconnectedHandler(MessagePayload<PlayerDisconnected> obj)
    {
        var playerId = obj.What.PlayerId;

        if (ConnectionStates.TryRemove(playerId, out IConnectionLogic logic))
        {
            logic.Dispose();
        }

        BroadcastLoadingStateIfChanged();
    }

    internal void ConnectionStateChangedHandler(MessagePayload<ConnectionStateChanged> obj)
    {
        BroadcastLoadingStateIfChanged();
    }

    /// <summary>
    /// Publishes a <see cref="LoadingPlayersChanged"/> whenever the number of loading connections
    /// differs from what was last broadcast. Concentrating this here makes the registry the single
    /// source of truth for "who is loading" and gives downstream handlers one event to react to.
    /// </summary>
    private void BroadcastLoadingStateIfChanged()
    {
        var loadingCount = LoadingPeers.Count();
        if (loadingCount == lastBroadcastLoadingCount) return;

        lastBroadcastLoadingCount = loadingCount;
        messageBroker.Publish(this, new LoadingPlayersChanged(loadingCount));
    }

    public IEnumerator<IConnectionState> GetEnumerator() => ConnectionStates.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
