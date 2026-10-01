using LiteNetLib;

namespace GameInterface.Services.Players;

public static class PlayerManagerExtensions
{
    public static bool ValidateHeroSender(this IPlayerManager playerManager, object sender, string requestedHeroId)
    {
        return !string.IsNullOrEmpty(requestedHeroId) &&
            sender is NetPeer peer &&
            playerManager.TryGetPlayer(peer, out var player) &&
            player?.HeroId == requestedHeroId;
    }
}
