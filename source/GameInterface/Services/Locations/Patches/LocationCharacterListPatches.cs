using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Patches for the methods mutating <see cref="Location"/>'s character list. The server broadcasts
/// every mutation; clients are blocked from mutating hero entries (those are synced strictly) but
/// keep mutating non-hero ambience locally, because the crowd-spawning behaviors are
/// scene-dependent and can only run on the machine whose player is visiting.
/// </summary>
[HarmonyPatch(typeof(Location))]
internal class LocationCharacterListPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<Location>();

    [HarmonyPatch(nameof(Location.AddCharacter))]
    [HarmonyPrefix]
    static bool AddCharacterPrefix(Location __instance, LocationCharacter locationCharacter)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (locationCharacter?.Character == null) return true;

        if (ModInformation.IsClient)
        {
            if (locationCharacter.Character.IsHero == false) return true;

            // Expected during normal client visits; the synced entry arrives from the server instead.
            Logger.Debug("Client add of hero {Hero} to location {Location} blocked",
                locationCharacter.Character.StringId, __instance.StringId);
            return false;
        }

        MessageBroker.Instance.Publish(__instance,
            LocationCharacterFactory.CreateAddedEvent(__instance, locationCharacter));

        return true;
    }

    [HarmonyPatch(nameof(Location.RemoveLocationCharacter))]
    [HarmonyPrefix]
    static bool RemoveLocationCharacterPrefix(Location __instance, LocationCharacter locationCharacter)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (locationCharacter?.Character == null) return true;

        if (ModInformation.IsClient)
        {
            if (locationCharacter.Character.IsHero == false) return true;

            Logger.Debug("Client removal of hero {Hero} from location {Location} blocked",
                locationCharacter.Character.StringId, __instance.StringId);
            return false;
        }

        MessageBroker.Instance.Publish(__instance,
            new LocationCharacterRemoved(__instance, locationCharacter.Character));

        return true;
    }

    [HarmonyPatch(nameof(Location.RemoveAllCharacters), new Type[0])]
    [HarmonyPrefix]
    static bool RemoveAllCharactersPrefix(Location __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Clients clear locally (leaving a settlement clears temp characters); their synced
        // entries are rebuilt from the roster snapshot on the next settlement entry.
        if (ModInformation.IsClient) return true;

        MessageBroker.Instance.Publish(__instance, new AllLocationCharactersRemoved(__instance));

        return true;
    }

    // The predicate cannot be serialized (and the prison filter references Hero.MainHero, which
    // differs per machine), so the server snapshots the list and broadcasts the exact removals.
    [HarmonyPatch(nameof(Location.RemoveAllCharacters), typeof(Predicate<LocationCharacter>))]
    [HarmonyPrefix]
    static bool RemoveAllCharactersPredicatePrefix(Location __instance, ref List<LocationCharacter> __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return true;

        __state = __instance.GetCharacterList()?.ToList();
        return true;
    }

    [HarmonyPatch(nameof(Location.RemoveAllCharacters), typeof(Predicate<LocationCharacter>))]
    [HarmonyPostfix]
    static void RemoveAllCharactersPredicatePostfix(Location __instance, List<LocationCharacter> __state)
    {
        PublishRemovedDiff(__instance, __state);
    }

    [HarmonyPatch(nameof(Location.RemoveAllHeroCharactersFromPrison))]
    [HarmonyPrefix]
    static bool RemoveAllHeroCharactersFromPrisonPrefix(Location __instance, ref List<LocationCharacter> __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return true;

        __state = __instance.GetCharacterList()?.ToList();
        return true;
    }

    [HarmonyPatch(nameof(Location.RemoveAllHeroCharactersFromPrison))]
    [HarmonyPostfix]
    static void RemoveAllHeroCharactersFromPrisonPostfix(Location __instance, List<LocationCharacter> __state)
    {
        PublishRemovedDiff(__instance, __state);
    }

    private static void PublishRemovedDiff(Location location, List<LocationCharacter> before)
    {
        if (before == null) return;

        var remaining = new HashSet<LocationCharacter>(
            location.GetCharacterList() ?? Enumerable.Empty<LocationCharacter>());

        foreach (var removed in before.Where(entry => remaining.Contains(entry) == false))
        {
            if (removed?.Character == null) continue;

            MessageBroker.Instance.Publish(location, new LocationCharacterRemoved(location, removed.Character));
        }
    }

    public static void AddLocationCharacter(Location location, LocationCharacter locationCharacter)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                AddEntry(location, locationCharacter);
            }
        });
    }

    /// <summary>
    /// Adds an entry on the main thread under an allowed scope. Vanilla AddCharacter dereferences
    /// the owner complex for hero entries, so an ownerless network-created location adds to the
    /// list directly; owned locations go through ChangeLocation, which also notifies a running
    /// mission at this location so the new arrival is spawned into the scene immediately.
    /// </summary>
    internal static void AddEntry(Location location, LocationCharacter locationCharacter)
    {
        if (location._ownerComplex == null)
        {
            location._characterList ??= new List<LocationCharacter>();
            location._characterList.Add(locationCharacter);
        }
        else
        {
            location._ownerComplex.ChangeLocation(locationCharacter, null, location);
        }

        // Registered after the add so a throwing mutator cannot leave a synced-but-absent entry.
        SyncedLocationCharacters.Register(locationCharacter);
    }

    public static void RemoveLocationCharacter(Location location, CharacterObject character)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var entry = SyncedLocationCharacters.Find(location, character);
                if (entry == null)
                {
                    Logger.Debug("No roster entry for {Character} in location {Location}",
                        character?.StringId, location?.StringId);
                    return;
                }

                RemoveEntry(location, entry);
            }
        });
    }

    /// <summary>
    /// Removes an entry on the main thread under an allowed scope, notifying a running location
    /// mission when one is active. ChangeLocation with a null target dereferences the location
    /// encounter whenever any mission runs, so missions without one (field battles) fall back to
    /// the plain removal.
    /// </summary>
    internal static void RemoveEntry(Location location, LocationCharacter entry)
    {
        SyncedLocationCharacters.Unregister(entry);

        if (location._ownerComplex == null ||
            (CampaignMission.Current != null && PlayerEncounter.LocationEncounter == null))
        {
            location.RemoveLocationCharacter(entry);
            return;
        }

        location._ownerComplex.ChangeLocation(entry, location, null);
    }

    public static void RemoveAllLocationCharacters(Location location)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                foreach (var entry in location.GetCharacterList()?.ToList() ?? new List<LocationCharacter>())
                {
                    SyncedLocationCharacters.Unregister(entry);
                }

                location.RemoveAllCharacters();
            }
        });
    }
}
