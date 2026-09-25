using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Reads presentation fields from authoritative campaign objects on the game thread.</summary>
public interface IPlayerActivityReader : IGameAbstraction
{
    PlayerListEntry Read(Player player, bool online);
}

/// <inheritdoc cref="IPlayerActivityReader"/>
public sealed class PlayerActivityReader : IPlayerActivityReader
{
    private readonly IObjectManager objects;
    private readonly Dictionary<string, Vec2> previousPositions = new Dictionary<string, Vec2>();

    // Supplies the registry used to resolve each player's current hero and party.
    public PlayerActivityReader(IObjectManager objects) => this.objects = objects;

    // Projects one registration without retaining another roster or reading client-local encounters.
    public PlayerListEntry Read(Player player, bool online)
    {
        objects.TryGetObject<Hero>(player.HeroId, out var hero);
        objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
        var effectiveParty = party?.AttachedTo ?? party;
        bool moving = ObserveMovement(player.ControllerId,
            online && effectiveParty != null ? effectiveParty.Position.ToVec2() : (Vec2?)null);
        return new PlayerListEntry
        {
            ControllerId = player.ControllerId,
            PlatformName = player.PlatformName ?? string.Empty,
            HeroName = hero?.Name?.ToString() ?? string.Empty,
            Online = online,
            Activity = online ? ReadActivity(effectiveParty, moving) : PlayerActivity.None
        };
    }

    // Resolves encounter precedence before settlement and effective army movement.
    internal PlayerActivity ReadActivity(MobileParty party, bool moving)
    {
        if (party == null) return PlayerActivity.None;
        var effectiveParty = party.AttachedTo ?? party;
        var battle = effectiveParty.MapEvent;
        if (battle?.IsHideoutBattle == true) return PlayerActivity.Hideout;
        if (battle != null && (battle.IsSiegeAssault || battle.IsSiegeOutside ||
            battle.IsSallyOut || battle.IsSiegeAmbush || battle.IsBlockade || battle.IsBlockadeSallyOut))
            return PlayerActivity.Siege;
        if (battle != null) return PlayerActivity.Battle;
        if (effectiveParty.BesiegerCamp != null || effectiveParty.CurrentSettlement?.SiegeEvent != null)
            return PlayerActivity.Siege;
        var settlement = effectiveParty.CurrentSettlement;
        if (settlement?.IsTown == true) return PlayerActivity.Town;
        if (settlement?.IsVillage == true) return PlayerActivity.Village;
        if (settlement?.IsCastle == true) return PlayerActivity.Castle;
        return moving ? PlayerActivity.Travelling : PlayerActivity.Idle;
    }

    // Measures displacement between regular samples, not a pending movement order or target.
    internal bool ObserveMovement(string controllerId, Vec2? position)
    {
        if (!position.HasValue)
        {
            previousPositions.Remove(controllerId);
            return false;
        }
        bool moving = previousPositions.TryGetValue(controllerId, out var previous) &&
            (position.Value - previous).LengthSquared > 0.000001f;
        previousPositions[controllerId] = position.Value;
        return moving;
    }
}
