using Common.Logging;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

/// <summary>
/// Binds an NPC puppet spawn record's roster identity to a LOCAL <see cref="LocationCharacter"/>
/// entry (SR-022/R1): an existing not-yet-agent-bound entry for the same character first (covers the
/// server-synced hero roster and any locally present fixed entries), else a reconstructed entry
/// added straight to the location's character list. Building the puppet from the chosen entry's
/// <c>AgentData.AgentOrigin</c> makes it a first-class citizen of native bookkeeping on every client
/// — <c>IsAlreadySpawned</c> (origin reference identity, V4), <c>GetLocationCharacter(agent.Origin)</c>
/// lookups, and passage guards — which is what makes adopt-in-place migration exact (SR-030/SR-032).
/// </summary>
public interface ILocationPuppetRosterBinder : IGameAbstraction
{
    /// <summary>
    /// [Game thread] Resolve or reconstruct the local roster entry for a puppet record. False when
    /// the location or character cannot resolve — the caller falls back to an unbound build.
    /// </summary>
    bool TryBindOrReconstruct(LocationCharacterData rosterData, out LocationCharacter entry);

    /// <summary>
    /// [Host, game thread] Extract the serializable roster identity of a captured agent — the
    /// <see cref="LocationCharacter"/> entry of the CURRENT mission location whose origin the agent
    /// carries (native spawns build agents from their entry's <c>AgentData</c>, so origin reference
    /// identity finds it). False when the agent has no roster entry (record ships unbound).
    /// </summary>
    bool TryExtractRosterData(Agent agent, out LocationCharacterData rosterData);

    /// <summary>
    /// [Game thread] Replicate a host-side passage move (SR-026): remove the agent's bound roster
    /// entry from <paramref name="sourceLocationId"/>'s list and add it to
    /// <paramref name="destinationLocationId"/>'s — the local mirror of what vanilla
    /// <c>ChangeLocation</c> did on the host, without its mission notifications. Keeps a later
    /// promoted host's passage rosters truthful so the NPC can be selected to wander back.
    /// </summary>
    bool TryMoveBoundEntry(Agent agent, string sourceLocationId, string destinationLocationId);
}

/// <inheritdoc cref="ILocationPuppetRosterBinder"/>
internal class LocationPuppetRosterBinder : ILocationPuppetRosterBinder
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationPuppetRosterBinder>();

    private readonly IObjectManager objectManager;

    public LocationPuppetRosterBinder(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryBindOrReconstruct(LocationCharacterData rosterData, out LocationCharacter entry)
    {
        entry = null;
        if (rosterData == null) return false;

        if (!objectManager.TryGetObject<Location>(rosterData.LocationId, out var location) || location == null)
        {
            Logger.Warning("[LocationNpc] Cannot bind puppet roster entry: location {LocationId} unresolved", rosterData.LocationId);
            return false;
        }

        if (!objectManager.TryGetObject<CharacterObject>(rosterData.CharacterId, out var character) || character == null)
        {
            Logger.Warning("[LocationNpc] Cannot bind puppet roster entry: character {CharacterId} unresolved", rosterData.CharacterId);
            return false;
        }

        // Prefer an existing local entry for this character that no live agent is bound to yet —
        // the server-synced hero roster (notables/companions/prisoners) and fixed characters land
        // here, so their puppets attach to exactly the entry the rest of the sync stack manages.
        foreach (var candidate in location.GetCharacterList() ?? (IEnumerable<LocationCharacter>)new List<LocationCharacter>())
        {
            if (candidate?.Character != character) continue;
            if (IsBoundToLiveAgent(candidate)) continue;

            entry = candidate;
            return true;
        }

        // No local entry (ambient crowd — non-hosts never roll their own, R1): reconstruct one from
        // the record and add it DIRECTLY to the character list. No ChangeLocation notify (the puppet
        // spawner owns the spawning) and no SyncedLocationCharacters registration (the server's
        // roster reconcile must never treat a puppet-reconstructed ambient entry as server-owned).
        MobileParty originParty = null;
        if (!string.IsNullOrEmpty(rosterData.OriginPartyId))
            objectManager.TryGetObject(rosterData.OriginPartyId, out originParty);

        ItemObject specialItem = null;
        if (!string.IsNullOrEmpty(rosterData.SpecialItemId))
            objectManager.TryGetObject(rosterData.SpecialItemId, out specialItem);

        entry = LocationCharacterFactory.Create(
            character,
            originParty,
            specialItem,
            rosterData.SpawnTag,
            rosterData.ActionSetCode,
            rosterData.BehaviorsMethodName,
            rosterData.CharacterRelation,
            rosterData.FixedLocation,
            rosterData.UseCivilianEquipment,
            rosterData.PrefabBones,
            rosterData.PrefabNames);

        location._characterList ??= new List<LocationCharacter>();
        location._characterList.Add(entry);
        return true;
    }

    public bool TryExtractRosterData(Agent agent, out LocationCharacterData rosterData)
    {
        rosterData = null;

        var location = CampaignMission.Current?.Location;
        if (agent?.Origin == null || location == null) return false;

        var entry = location.GetLocationCharacter(agent.Origin);
        if (entry == null) return false;

        if (!objectManager.TryGetId(location, out var locationId)) return false;

        return LocationCharacterFactory.TryCreateData(objectManager, locationId, entry, out rosterData);
    }

    public bool TryMoveBoundEntry(Agent agent, string sourceLocationId, string destinationLocationId)
    {
        if (agent?.Origin == null) return false;

        if (!objectManager.TryGetObjectWithLogging<Location>(sourceLocationId, out var source) || source == null)
            return false;
        if (!objectManager.TryGetObjectWithLogging<Location>(destinationLocationId, out var destination) || destination == null)
            return false;

        LocationCharacter entry = null;
        foreach (var candidate in source.GetCharacterList() ?? (IEnumerable<LocationCharacter>)new List<LocationCharacter>())
        {
            if (candidate?.AgentOrigin != agent.Origin) continue;
            entry = candidate;
            break;
        }

        if (entry == null)
        {
            Logger.Debug("[LocationNpc] Passage move: no bound entry for the agent in {Source} — nothing to move", sourceLocationId);
            return false;
        }

        source._characterList?.Remove(entry);
        destination._characterList ??= new List<LocationCharacter>();
        destination._characterList.Add(entry);
        return true;
    }

    // An entry is bound when a live mission agent carries its origin — the same reference-identity
    // rule native bookkeeping uses (MissionAgentHandler.IsAlreadySpawned).
    private static bool IsBoundToLiveAgent(LocationCharacter candidate)
    {
        var mission = Mission.Current;
        if (mission == null) return false;

        foreach (var agent in mission.Agents)
        {
            if (agent.Origin == candidate.AgentOrigin)
                return true;
        }
        return false;
    }
}
