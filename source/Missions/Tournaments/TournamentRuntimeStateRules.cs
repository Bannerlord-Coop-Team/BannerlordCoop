using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace Missions.Tournaments;

public static class TournamentRuntimeStateRules
{
    private const int MaximumRuntimeAgents = 32;
    private const int MaximumWorldItems = 256;
    private const int ValidWeaponSpawnFlags = 127;

    public static IReadOnlyDictionary<Guid, TournamentAgentRuntimeData> GetAgents(
        NetworkTournamentRuntimeState state)
    {
        var agents = new Dictionary<Guid, TournamentAgentRuntimeData>();
        if (state == null) return agents;

        int inspectedAgents = 0;
        foreach (TournamentAgentRuntimeData data in state.Agents ?? Array.Empty<TournamentAgentRuntimeData>())
        {
            if (++inspectedAgents > MaximumRuntimeAgents) break;
            if (data == null || data.AgentId == Guid.Empty || data.Health <= 0 ||
                float.IsNaN(data.Health) || float.IsInfinity(data.Health)) continue;
            if (!agents.ContainsKey(data.AgentId))
                agents.Add(data.AgentId, data);
        }
        return agents;
    }

    public static IReadOnlyDictionary<Guid, float> GetAgentHealth(NetworkTournamentRuntimeState state)
    {
        var healthByAgent = new Dictionary<Guid, float>();
        foreach (var pair in GetAgents(state))
            healthByAgent.Add(pair.Key, pair.Value.Health);
        return healthByAgent;
    }

    public static IReadOnlyDictionary<Guid, TournamentWorldItemRuntimeData> GetWorldItems(
        NetworkTournamentRuntimeState state)
    {
        var worldItems = new Dictionary<Guid, TournamentWorldItemRuntimeData>();
        if (state == null) return worldItems;

        int inspectedWorldItems = 0;
        foreach (TournamentWorldItemRuntimeData data in
                 state.WorldItems ?? Array.Empty<TournamentWorldItemRuntimeData>())
        {
            if (++inspectedWorldItems > MaximumWorldItems) break;
            if (data == null || data.WorldItemId == Guid.Empty || string.IsNullOrEmpty(data.ItemId) ||
                data.ItemId.Length > 256 || (data.ItemModifierId?.Length ?? 0) > 256 ||
                (data.BannerCode?.Length ?? 0) > 4096 ||
                (data.SpawnFlags & ~ValidWeaponSpawnFlags) != 0 ||
                !IsFinite(data.Position) || !IsFinite(data.Rotation)) continue;
            if (!worldItems.ContainsKey(data.WorldItemId))
                worldItems.Add(data.WorldItemId, data);
        }
        return worldItems;
    }

    public static Guid[] GetMissingAgentIds(
        TournamentSpawnManifestData manifest,
        NetworkTournamentRuntimeState state)
    {
        if (manifest == null) return Array.Empty<Guid>();
        IReadOnlyDictionary<Guid, float> healthByAgent = GetAgentHealth(state);
        return (manifest.Agents ?? Array.Empty<TournamentAgentSpawnData>())
            .Where(data => data != null)
            .SelectMany(data => data.MountAgentId == Guid.Empty
                ? new[] { data.AgentId }
                : new[] { data.AgentId, data.MountAgentId })
            .Where(agentId => agentId != Guid.Empty && !healthByAgent.ContainsKey(agentId))
            .Distinct()
            .ToArray();
    }

    private static bool IsFinite(Mat3 value) =>
        IsFinite(value.s) && IsFinite(value.f) && IsFinite(value.u);

    private static bool IsFinite(Vec3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
