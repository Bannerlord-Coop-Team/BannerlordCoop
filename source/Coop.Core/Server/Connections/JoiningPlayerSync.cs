using System;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Players;
using LiteNetLib;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Helpers for bringing a joining connection up to date with the rest of the session.
/// </summary>
internal static class JoiningPlayerSync
{
    /// <summary>
    /// Sends every already-registered player (except the joiner itself and the host) to a peer that has
    /// just begun its save transfer, so a late joiner learns about clients that connected before it.
    /// </summary>
    /// <remarks>
    /// Must be called after the peer has entered the connection message queue's Queueing phase (i.e. after
    /// the transfer-save snapshot): these go through <see cref="INetwork.Send"/> so they are held and
    /// replayed once the client enters its campaign. The pre-existing heroes are already in the transferred
    /// save, so the message carries no hero blob — the joiner only registers them as controlled, sharing the
    /// same <see cref="NetworkNewPlayerHeroCreated"/> path as a player who joins later (which does carry one).
    /// </remarks>
    public static void SendExistingPlayers(
        INetwork network,
        IPlayerManager playerManager,
        NetPeer joiner,
        string joinerControllerId)
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
