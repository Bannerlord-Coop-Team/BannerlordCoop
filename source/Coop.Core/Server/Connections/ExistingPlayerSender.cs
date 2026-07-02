using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Tells a joining peer about every player that already exists on the server, so it learns about everyone
/// who connected before it. Shared by the connection states that admit a peer — the existing-character
/// (<see cref="States.ResolveCharacterState"/>) and new-character (<see cref="States.CreateCharacterState"/>)
/// paths — both of which run this once the save snapshot has been taken and the peer's broadcasts are queued.
/// </summary>
public interface IExistingPlayerSender
{
    /// <summary>
    /// Send <paramref name="joiner"/> a <see cref="NetworkNewPlayerHeroCreated"/> for each already-existing
    /// player, excluding its own player (it registers itself on load) and the host (the server is not a
    /// controlled player on clients).
    /// </summary>
    void SendExistingPlayers(NetPeer joiner, string joinerControllerId);
}

/// <inheritdoc cref="IExistingPlayerSender"/>
public class ExistingPlayerSender : IExistingPlayerSender
{
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    public ExistingPlayerSender(IPlayerManager playerManager, INetwork network)
    {
        this.playerManager = playerManager;
        this.network = network;
    }

    public void SendExistingPlayers(NetPeer joiner, string joinerControllerId)
    {
        foreach (var player in playerManager.Players)
        {
            // Skip the joiner's own player (it registers itself on load) and the host (the server is not a
            // controlled player on clients).
            if (player.ControllerId == joinerControllerId) continue;
            if (player.ControllerId == CoopServer.ServerControllerId) continue;

            network.Send(joiner, new NetworkNewPlayerHeroCreated(player.ControllerId, player, Array.Empty<byte>()));
        }
    }
}
