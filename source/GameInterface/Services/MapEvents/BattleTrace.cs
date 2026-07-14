using Common;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Shared, null-safe helpers for the battle/encounter diagnostic traces that several map-event patches and
/// handlers emit. They previously copy-pasted the same party/map-event describers and the NRE-guarded
/// PlayerEncounter accessors; centralising them keeps every trace line byte-identical.
/// </summary>
internal static class BattleTrace
{
    /// <summary>Prefers the coop object-manager id; falls back to the party's StringId / name for unregistered parties.</summary>
    public static string DescribePartyForTrace(PartyBase party)
    {
        if (party == null)
            return "<null>";

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) && objectManager.TryGetId(party, out var partyId))
            return partyId;

        return party.MobileParty?.StringId ?? party.Name?.ToString() ?? "<unregistered-party>";
    }

    /// <summary>Prefers the coop object-manager id; falls back to the map event's StringId for unregistered events.</summary>
    public static string DescribeMapEventForTrace(MapEvent mapEvent)
    {
        if (mapEvent == null)
            return "<null>";

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) && objectManager.TryGetId(mapEvent, out var mapEventId))
            return mapEventId;

        return mapEvent.StringId ?? "<unregistered-map-event>";
    }

    /// <summary><see cref="PlayerEncounter.Battle"/> guarded against the NRE it throws when there is no active encounter.</summary>
    public static MapEvent GetPlayerEncounterBattleForTrace()
    {
        try
        {
            return PlayerEncounter.Battle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    /// <summary><see cref="PlayerEncounter.EncounteredBattle"/> guarded against the NRE it throws when there is no active encounter.</summary>
    public static MapEvent GetPlayerEncounterEncounteredBattleForTrace()
    {
        try
        {
            return PlayerEncounter.EncounteredBattle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// Best-effort "which map event is the local player in" for a trace line.
    /// When <paramref name="includeEncounterMapEvent"/> is set, the active encounter's own map event is preferred
    /// (used by the PlayerEncounter patches, which run while the encounter is being built).
    /// </summary>
    public static MapEvent GetCurrentMapEventForTrace(bool includeEncounterMapEvent = false)
    {
        if (includeEncounterMapEvent)
            return PlayerEncounter.Current?._mapEvent ?? GetPlayerEncounterBattleForTrace() ?? MobileParty.MainParty?.MapEvent;

        return GetPlayerEncounterBattleForTrace() ?? MobileParty.MainParty?.MapEvent;
    }
}
