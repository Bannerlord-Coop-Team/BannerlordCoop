using LiteNetLib;

namespace GameInterface.Services.Players;

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

    public static bool ValidateHeroSender(this IPlayerManager playerManager, object sender, string requestedHeroId)
    {
        return !string.IsNullOrEmpty(requestedHeroId) &&
            sender is NetPeer peer &&
            playerManager.TryGetPlayer(peer, out var player) &&
            player?.HeroId == requestedHeroId;
    }
}
