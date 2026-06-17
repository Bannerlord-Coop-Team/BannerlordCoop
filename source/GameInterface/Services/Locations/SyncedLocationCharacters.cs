using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations;

/// <summary>
/// Client-side bookkeeping of which <see cref="LocationCharacter"/> entries were created from
/// server broadcasts. Clients may also hold locally generated ambient entries (non-hero crowd
/// spawned by mission behaviors), and roster reconciliation must only ever touch the synced ones.
/// </summary>
internal static class SyncedLocationCharacters
{
    private static readonly ConditionalWeakTable<LocationCharacter, object> syncedCharacters = new ConditionalWeakTable<LocationCharacter, object>();

    public static void Register(LocationCharacter locationCharacter)
    {
        if (locationCharacter == null) return;

        syncedCharacters.Remove(locationCharacter);
        syncedCharacters.Add(locationCharacter, null);
    }

    public static void Unregister(LocationCharacter locationCharacter)
    {
        if (locationCharacter == null) return;

        syncedCharacters.Remove(locationCharacter);
    }

    public static bool IsSynced(LocationCharacter locationCharacter)
    {
        return locationCharacter != null && syncedCharacters.TryGetValue(locationCharacter, out _);
    }

    /// <summary>
    /// Finds a roster entry for the given character template, preferring synced entries so that a
    /// broadcast removal never consumes a locally generated ambient entry of the same template.
    /// Falls back to any matching entry for server-side use, where nothing is registered as synced.
    /// </summary>
    public static LocationCharacter Find(Location location, CharacterObject character)
    {
        var characterList = location?.GetCharacterList();
        if (characterList == null) return null;

        return characterList.FirstOrDefault(entry => entry.Character == character && IsSynced(entry))
            ?? characterList.FirstOrDefault(entry => entry.Character == character);
    }
}
