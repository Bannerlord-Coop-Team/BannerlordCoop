using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// BR-110: the Bannerlord engine can only render a maximum of <see cref="MaxRenderedAgents"/> agents, so no
/// coop spawn path may push a mission's live agent count — humans and mounts alike — past that ceiling. Spawn
/// systems consult the remaining capacity before spawning and defer what does not fit.
/// </summary>
public interface IBattleAgentBudget
{
    /// <summary>The engine's rendered-agent ceiling (BR-110).</summary>
    int MaxRenderedAgents { get; }

    /// <summary>Active agents currently in the mission — humans AND mounts, since both occupy render slots.</summary>
    int CountLiveAgents(Mission mission);

    /// <summary>Spawnable slots left under the engine limit for a given live agent count.</summary>
    int RemainingCapacity(int liveAgents);

    /// <summary>Whether the mission can fit <paramref name="slotsNeeded"/> more agents (a mounted troop and
    /// its horse need two).</summary>
    bool HasCapacityFor(Mission mission, int slotsNeeded);

    /// <summary>Clamp a spawn request to what the mission can still fit.</summary>
    int ClampToCapacity(Mission mission, int requested);

    /// <summary>Render slots one spawn consumes: 2 when the equipment mounts a horse (the engine spawns the
    /// rider and its mount together in a single <see cref="Mission.SpawnAgent"/> call), else 1.</summary>
    int SlotsForEquipment(Equipment equipment);

    /// <summary>Render slots spawning <paramref name="origin"/> consumes — <see cref="SlotsForEquipment"/> of
    /// the equipment the origin's troop fights in (a hero's battle equipment, a regular's character
    /// equipment). A null origin spawns nothing and costs 0.</summary>
    int SlotsForOrigin(IAgentOriginBase origin);
}

/// <inheritdoc cref="IBattleAgentBudget"/>
/// <remarks>
/// Stateless — it reads the live agent count off the mission each call — so its DI lifetime is moot. Spawn
/// systems consult it so puppets stay buffered and re-drain, withheld reinforcement troops re-field on tick,
/// and unallocated wave troops stay unsupplied for the native wave logic to re-request as casualties free
/// slots. A null mission — no engine to overload, e.g. a supplier driven outside a mission — always has
/// capacity and never clamps.
/// </remarks>
public class BattleAgentBudget : IBattleAgentBudget
{
    /// <inheritdoc/>
    public int MaxRenderedAgents => 2000;

    /// <inheritdoc/>
    public int CountLiveAgents(Mission mission)
    {
        var agents = mission?.Agents;
        if (agents == null) return 0;

        int live = 0;
        foreach (var agent in agents)
            if (agent != null && agent.IsActive()) live++;
        return live;
    }

    /// <inheritdoc/>
    public int RemainingCapacity(int liveAgents) => Math.Max(0, MaxRenderedAgents - liveAgents);

    /// <inheritdoc/>
    public bool HasCapacityFor(Mission mission, int slotsNeeded)
        => mission == null || RemainingCapacity(CountLiveAgents(mission)) >= slotsNeeded;

    /// <inheritdoc/>
    public int ClampToCapacity(Mission mission, int requested)
        => mission == null ? requested : Math.Min(requested, RemainingCapacity(CountLiveAgents(mission)));

    /// <inheritdoc/>
    public int SlotsForEquipment(Equipment equipment)
    {
        // A real mount is a Horse-slot item carrying a HorseComponent — the same signal the engine uses to
        // spawn the rider's mount. (Checking only that the slot is non-empty would miscount, since not every
        // Horse-slot item is a ridable mount.)
        var horse = equipment?.Horse.Item;
        return horse != null && horse.HasHorseComponent ? 2 : 1;
    }

    /// <inheritdoc/>
    public int SlotsForOrigin(IAgentOriginBase origin)
    {
        if (origin == null) return 0;

        // A troop outside the campaign type system resolves no equipment and costs the rider-only minimum.
        var character = origin.Troop as CharacterObject;
        var equipment = character == null
            ? null
            : (character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment);
        return SlotsForEquipment(equipment);
    }
}
