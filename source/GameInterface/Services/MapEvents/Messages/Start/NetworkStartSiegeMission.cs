using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Opens the walls-assault siege mission on the requester. Carries the mission-defining siege inputs
/// snapshotted once per map event on the server, so every entrant loads a physically identical scene
/// even if campaign-side bombardment sync is mid-flight on their machine.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkStartSiegeMission : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public int WallLevel { get; }
    [ProtoMember(3)]
    public float[] WallHitPointRatios { get; }
    [ProtoMember(4)]
    public SiegeEngineState[] AttackerEngines { get; }
    [ProtoMember(5)]
    public SiegeEngineState[] DefenderEngines { get; }

    public NetworkStartSiegeMission(string mapEventId, int wallLevel, float[] wallHitPointRatios,
        SiegeEngineState[] attackerEngines, SiegeEngineState[] defenderEngines)
    {
        MapEventId = mapEventId;
        WallLevel = wallLevel;
        WallHitPointRatios = wallHitPointRatios;
        AttackerEngines = attackerEngines;
        DefenderEngines = defenderEngines;
    }
}

/// <summary>
/// One deployed siege engine as the mission reads it: type, deployment slot, and remaining health.
/// Public because the mission host (Missions assembly) also reports final engine states with it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record SiegeEngineState
{
    [ProtoMember(1)]
    public string EngineTypeId { get; }
    [ProtoMember(2)]
    public int Index { get; }
    [ProtoMember(3)]
    public float Health { get; }
    [ProtoMember(4)]
    public float MaxHealth { get; }

    public SiegeEngineState(string engineTypeId, int index, float health, float maxHealth)
    {
        EngineTypeId = engineTypeId;
        Index = index;
        Health = health;
        MaxHealth = maxHealth;
    }
}
