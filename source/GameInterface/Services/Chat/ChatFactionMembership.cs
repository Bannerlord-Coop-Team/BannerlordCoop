using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Chat;

public interface IChatFactionMembership
{
    bool TryGetClan(Player player, out Clan clan);
    bool TryGetKingdom(Player player, out Kingdom kingdom);
}

/// <summary>
/// Live clan/kingdom from the player's hero. Player.ClanId stays at join time and goes stale after JoinClan.
/// </summary>
public sealed class ChatFactionMembership : IChatFactionMembership
{
    private readonly IObjectManager objectManager;

    public ChatFactionMembership(IObjectManager objectManager)
    {
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));
        this.objectManager = objectManager;
    }

    public bool TryGetClan(Player player, out Clan clan)
    {
        clan = null;
        if (string.IsNullOrEmpty(player?.HeroId)) return false;
        if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || hero?.Clan == null)
            return false;

        clan = hero.Clan;
        return true;
    }

    public bool TryGetKingdom(Player player, out Kingdom kingdom)
    {
        kingdom = null;
        if (!TryGetClan(player, out var clan) || clan.Kingdom == null) return false;

        kingdom = clan.Kingdom;
        return true;
    }
}
