using GameInterface.Services.Clans;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Players;

public static class PlayerManagerExtensions
{
    // True when the sending peer's hero may manage the clan (its leader).
    public static bool CanManageClan(this IPlayerManager playerManager, object sender, Clan clan,
        IObjectManager objectManager, ICoopClanPermissions permissions)
    {
        return sender is NetPeer peer && playerManager.TryGetPlayer(peer, out var player) &&
            objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var actor) &&
            permissions.CanManageClan(actor, clan);
    }
}
