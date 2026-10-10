using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Hideouts;

internal enum HideoutPhase
{
    Waiting,
    Stealth,
    Camp,
    CallingTroops,
    Cinematic,
    Conversation,
    Duel,
    BossBattle,
    Resolved,
}

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkHideoutState : IEvent
{
    [ProtoMember(1)] public string InstanceId { get; }
    [ProtoMember(2)] public string HostControllerId { get; }
    [ProtoMember(3)] public int HostEpoch { get; }
    [ProtoMember(4)] public long Revision { get; }
    [ProtoMember(5)] public HideoutPhase Phase { get; }
    [ProtoMember(6)] public int BossSeed { get; }
    [ProtoMember(7)] public int SpeakerSeed { get; }
    [ProtoMember(8)] public int[] SpawnedSeeds { get; }
    [ProtoMember(9)] public BattleSideEnum Winner { get; }
    [ProtoMember(10)] public int InitialPopulation { get; }
    [ProtoMember(11)] public int[] HiddenSeeds { get; }
    [ProtoMember(12)] public HideoutDefenderState[] Defenders { get; }
    [ProtoMember(13)] public int[] ActiveHeroSeeds { get; }

    public NetworkHideoutState(string instanceId, string hostControllerId, int hostEpoch,
        long revision, HideoutPhase phase, int bossSeed, int speakerSeed, int[] spawnedSeeds,
        BattleSideEnum winner, int initialPopulation, int[] hiddenSeeds = null, HideoutDefenderState[] defenders = null,
        int[] activeHeroSeeds = null)
    {
        InstanceId = instanceId;
        HostControllerId = hostControllerId;
        HostEpoch = hostEpoch;
        Revision = revision;
        Phase = phase;
        BossSeed = bossSeed;
        SpeakerSeed = speakerSeed;
        SpawnedSeeds = spawnedSeeds;
        Winner = winner;
        InitialPopulation = initialPopulation;
        HiddenSeeds = hiddenSeeds;
        Defenders = defenders;
        ActiveHeroSeeds = activeHeroSeeds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class HideoutDefenderState
{
    [ProtoMember(1)] public int Seed { get; }
    [ProtoMember(2)] public int WatchState { get; }
    [ProtoMember(3)] public bool Alarmed { get; }
    [ProtoMember(4)] public int StandingPointId { get; }
    [ProtoMember(5)] public bool Patrolling { get; }

    public HideoutDefenderState(int seed, int watchState, bool alarmed, int standingPointId, bool patrolling)
    {
        Seed = seed;
        WatchState = watchState;
        Alarmed = alarmed;
        StandingPointId = standingPointId;
        Patrolling = patrolling;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutCallTroops : IEvent
{
    [ProtoMember(1)] public string InstanceId { get; }
    [ProtoMember(2)] public string ControllerId { get; }
    [ProtoMember(3)] public int UsePointId { get; }

    public NetworkHideoutCallTroops(string instanceId, string controllerId, int usePointId)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
        UsePointId = usePointId;
    }
}
