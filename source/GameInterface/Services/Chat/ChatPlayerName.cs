using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Chat;

/// <summary>Resolves the campaign display name for a registered player.</summary>
public interface IChatPlayerNameResolver
{
    string Resolve(Player player);
}

/// <inheritdoc cref="IChatPlayerNameResolver"/>
public sealed class ChatPlayerName : IChatPlayerNameResolver
{
    private readonly IObjectManager objectManager;

    public ChatPlayerName(IObjectManager objectManager)
    {
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));

        this.objectManager = objectManager;
    }

    public string Resolve(Player player)
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
