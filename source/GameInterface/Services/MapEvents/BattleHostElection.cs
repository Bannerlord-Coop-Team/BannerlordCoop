using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Deterministic local computation of the battle host — the lowest controller id among the human parties in
/// the map event. This lets a client gate spawning at mission-open time (before the troops spawn) without
/// waiting for the server's authoritative <c>NetworkBattleHostAssigned</c> round-trip. It uses the same rule
/// as the server's election (BattleHostHandler), so the two agree; the server assignment remains the
/// authoritative record (and drives successors / migration).
/// </summary>
public static class BattleHostElection
{
    /// <returns>True/false if this client is the host, or null if no human participants are resolvable yet.</returns>
    public static bool? IsLocalHost(MapEvent mapEvent)
    {
        if (mapEvent == null) return null;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var players)) return null;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return null;

        string lowest = null;
        foreach (var player in players.Players)
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) continue;
            if (party?.MapEvent != mapEvent) continue;

            if (lowest == null || string.CompareOrdinal(player.ControllerId, lowest) < 0)
                lowest = player.ControllerId;
        }

        if (lowest == null) return null;
        return lowest == controllerIdProvider.ControllerId;
    }
}
