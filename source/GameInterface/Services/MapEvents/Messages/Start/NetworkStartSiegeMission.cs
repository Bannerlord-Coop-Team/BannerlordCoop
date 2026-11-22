using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Opens a walls-assault or siege-ambush mission on an authoritative participant. Carries the server's siege inputs,
/// rebuilt on each new mission claim and shared by every entrant of that mission so they load the same scene.
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
    [ProtoMember(6)]
    public string InitiatingPartyId { get; }
    [ProtoMember(7)]
    public bool IsSallyOut { get; }
    [ProtoMember(8)]
    public string SettlementId { get; }

    public NetworkStartSiegeMission(string mapEventId, int wallLevel, float[] wallHitPointRatios,
        SiegeEngineState[] attackerEngines, SiegeEngineState[] defenderEngines, string initiatingPartyId,
        string settlementId, bool isSallyOut = false)
    {
        MapEventId = mapEventId;
        WallLevel = wallLevel;
        WallHitPointRatios = wallHitPointRatios;
        AttackerEngines = attackerEngines;
        DefenderEngines = defenderEngines;
        InitiatingPartyId = initiatingPartyId;
        SettlementId = settlementId;
        IsSallyOut = isSallyOut;
    }
}

/// <summary>
/// One deployed siege engine as the mission reads it: type, position in the compact deployed-engine snapshot,
/// and remaining health.
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
