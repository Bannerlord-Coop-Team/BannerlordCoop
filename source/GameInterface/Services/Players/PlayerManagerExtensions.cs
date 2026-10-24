using LiteNetLib;

namespace GameInterface.Services.Players;

/// <summary>Peer-to-controller rules shared by handlers that act on a sender's player.</summary>
public static class PlayerManagerExtensions
{
    /// <summary>
    /// The controller <paramref name="peer"/> currently stands for; a stale peer of a player that reconnected stands for nobody.
    /// </summary>
    public static bool TryGetCurrentController(this IPlayerManager playerManager, NetPeer peer, out string controllerId)
    {
        controllerId = null;
        if (!playerManager.TryGetPlayer(peer, out var player) || !playerManager.TryGetPeer(player.ControllerId, out var currentPeer)
            || !ReferenceEquals(currentPeer, peer))
            return false;

        controllerId = player.ControllerId;
        return true;
    }
}
