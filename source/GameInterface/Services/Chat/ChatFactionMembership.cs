using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Chat;

public interface IChatFactionMembership
{
    IFaction GetClan(Player player);
    IFaction GetKingdom(Player player);
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

    public IFaction GetClan(Player player)
    {
        if (string.IsNullOrEmpty(player.HeroId)) return null;
        if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero)) return null;
        return hero.Clan;
    }

    public IFaction GetKingdom(Player player)
    {
        return GetClan(player) is Clan clan ? clan.Kingdom : null;
    }
}
