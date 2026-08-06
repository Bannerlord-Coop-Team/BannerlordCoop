using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Chat;

/// <summary>Resolves the campaign display name for a registered player.</summary>
public static class ChatPlayerName
{
    public static string Resolve(IObjectManager objectManager, Player player)
    {
        if (!string.IsNullOrEmpty(player?.HeroId) &&
            objectManager.TryGetObject<Hero>(player.HeroId, out var hero) &&
            hero?.Name != null)
        {
            string name = hero.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        return player?.ControllerId ?? string.Empty;
    }
}
