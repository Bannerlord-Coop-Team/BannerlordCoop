#if DEBUG
using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Battles.Messages;

public enum BattleGuardFixtureMode
{
    Foot,
    Mounted
}

public enum BattleGuardFixturePhase
{
    Calibration,
    Guard,
    Attack
}

public enum BattleGuardFixtureDirection
{
    Up,
    Down,
    Left,
    Right
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleGuardFixtureCommand : IEvent
{
    [ProtoMember(8)]
    public string BattleInstanceId { get; }

    [ProtoMember(1)]
    public Guid GuardAgentId { get; }

    [ProtoMember(2)]
    public string GuardAuthority { get; }

    [ProtoMember(3)]
    public Guid StrikerAgentId { get; }

    [ProtoMember(4)]
    public string StrikerAuthority { get; }

    [ProtoMember(5)]
    public BattleGuardFixtureMode Mode { get; }

    [ProtoMember(6)]
    public BattleGuardFixturePhase Phase { get; }

    [ProtoMember(7)]
    public bool Reset { get; }

    [ProtoMember(9)]
    public Guid CommandId { get; }

    [ProtoMember(10)]
    public BattleGuardFixtureDirection Direction { get; }

    public NetworkBattleGuardFixtureCommand(
        string battleInstanceId,
        Guid commandId,
        Guid guardAgentId,
        string guardAuthority,
        Guid strikerAgentId,
        string strikerAuthority,
        BattleGuardFixtureMode mode,
        BattleGuardFixturePhase phase,
        BattleGuardFixtureDirection direction =
            BattleGuardFixtureDirection.Up,
        bool reset = false)
    {
        BattleInstanceId = battleInstanceId;
        CommandId = commandId;
        GuardAgentId = guardAgentId;
        GuardAuthority = guardAuthority;
        StrikerAgentId = strikerAgentId;
        StrikerAuthority = strikerAuthority;
        Mode = mode;
        Phase = phase;
        Direction = direction;
        Reset = reset;
    }
}
#endif
