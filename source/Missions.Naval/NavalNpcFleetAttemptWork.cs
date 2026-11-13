using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>[Game thread] What one AI fleet attempt changed so far, so a failed attempt takes back exactly its own work.</summary>
public sealed class NavalNpcFleetAttemptWork
{
    /// <summary>Every hull the attempt spawned, in spawn order, whether or not it registered.</summary>
    public List<MissionObject> Hulls { get; } = new List<MissionObject>();

    /// <summary>The reserve troops the attempt pulled for its crew.</summary>
    public List<IAgentOriginBase> Troops { get; } = new List<IAgentOriginBase>();

    /// <summary>How many crew vanilla's first batch spawned (diagnostics).</summary>
    public int FirstCrewBatch { get; set; }

    /// <summary>Whether the troops were handed to vanilla's crewing, which then holds the ones not spawned yet.</summary>
    public bool CrewingStarted { get; private set; }

    /// <summary>The team side the crewing ran on.</summary>
    public TeamSideEnum CrewSide { get; private set; }

    /// <summary>Whether the side spawned reinforcements on tick before the crewing turned them on.</summary>
    public bool SpawnedOnTickBefore { get; private set; }

    /// <summary>Whether a failed attempt's troops were dropped from vanilla's crewing, so a repeated take-back skips it.</summary>
    public bool TroopsDropped { get; private set; }

    // Records that the failed attempt's waiting troops were dropped.
    public void MarkTroopsDropped() => TroopsDropped = true;

    // Marks the troops as handed to vanilla, with the reinforcement switch to put back if the attempt fails.
    public void BeginCrewing(TeamSideEnum side, bool spawnedOnTickBefore)
    {
        CrewingStarted = true;
        CrewSide = side;
        SpawnedOnTickBefore = spawnedOnTickBefore;
    }
#if DEBUG

    /// <summary>The stage a DEBUG fleet failure throws at in this attempt, if one was armed.</summary>
    public NavalFleetFailStage? ArmedFailure { get; set; }

    // Throws the armed failure once the attempt reaches its stage, through the attempt's normal failure path.
    public void ThrowIfArmed(NavalFleetFailStage stage)
    {
        if (ArmedFailure != stage) return;

        ArmedFailure = null;
        throw new InvalidOperationException($"DEBUG fleet_fail: forced AI fleet spawn failure ({stage})");
    }
#endif
}
