using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventParties;

/// <summary>
/// Helpers over a <see cref="MapEventParty"/>'s flattened battle roster (<c>_roster</c>). They live in
/// GameInterface because it is publicized — Missions references the raw game assemblies and cannot reach the
/// internal field.
/// </summary>
public static class BattleRosterHelper
{
    /// <summary>
    /// Find a live troop whose character has StringId <paramref name="troopCharacterId"/> in the party's
    /// flattened battle roster and return its CURRENT <see cref="UniqueTroopDescriptor"/>. This lets a casualty
    /// keyed by character be applied through <c>OnTroopKilled</c>/<c>OnTroopWounded</c> without the stale-seed
    /// <c>KeyNotFoundException</c> that descriptor churn (the engine re-flattening parties during setup) causes.
    /// When <paramref name="excludeWounded"/> is set (the casualty is a wound), already-wounded troops are
    /// skipped so a wound doesn't target a man who is already down.
    /// </summary>
    public static bool TryGetLiveDescriptor(MapEventParty party, string troopCharacterId, bool excludeWounded, out UniqueTroopDescriptor descriptor)
    {
        descriptor = default;
        if (party?._roster == null || string.IsNullOrEmpty(troopCharacterId)) return false;

        foreach (var element in party._roster)
        {
            if (element.IsKilled) continue;
            if (excludeWounded && element.IsWounded) continue;
            if (element.Troop?.StringId != troopCharacterId) continue;

            descriptor = element.Descriptor;
            return true;
        }

        return false;
    }
}
