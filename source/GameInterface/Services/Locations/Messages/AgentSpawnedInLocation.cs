using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// [Host, local] A non-player agent spawned in the active settlement location mission
/// (<c>Mission.SpawnAgent</c> capture — humans). Published by <c>LocationAgentSpawnedPatch</c>; the
/// location replicator registers + replicates it (SR-021).
/// </summary>
public readonly struct AgentSpawnedInLocation : IEvent
{
    public Agent Agent { get; }

    public AgentSpawnedInLocation(Agent agent)
    {
        Agent = agent;
    }
}

/// <summary>
/// [Host, local] An animal spawned in the active settlement location mission via
/// <c>Mission.SpawnMonster</c> — the animal path bypasses <c>SpawnAgent(AgentBuildData, bool)</c>
/// entirely (SR-021/V3), so it is captured separately with the item identities the receiver needs to
/// re-spawn the same monster.
/// </summary>
public readonly struct MonsterSpawnedInLocation : IEvent
{
    public Agent Agent { get; }

    /// <summary>ObjectManager id of the animal's item (e.g. "sheep", a horse item); null when unresolvable.</summary>
    public string ItemId { get; }

    /// <summary>ObjectManager id of the harness item; null when none or unresolvable.</summary>
    public string HarnessItemId { get; }

    public MonsterSpawnedInLocation(Agent agent, string itemId, string harnessItemId)
    {
        Agent = agent;
        ItemId = itemId;
        HarnessItemId = harnessItemId;
    }
}
