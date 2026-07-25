#if DEBUG
using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Battles.Messages;

/// <summary>Shares the mounted guard route selected by its authority.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleGuardFixtureRoute : IEvent
{
    [ProtoMember(1)]
    public string BattleInstanceId { get; }

    [ProtoMember(2)]
    public Guid GuardAgentId { get; }

    [ProtoMember(3)]
    public string GuardAuthority { get; }

    [ProtoMember(4)]
    public float StartX { get; }

    [ProtoMember(5)]
    public float StartY { get; }

    [ProtoMember(6)]
    public float StartZ { get; }

    [ProtoMember(7)]
    public float DirectionX { get; }

    [ProtoMember(8)]
    public float DirectionY { get; }

    [ProtoMember(9)]
    public float Length { get; }

    [ProtoMember(10)]
    public BattleGuardFixturePhase Phase { get; }

    [ProtoMember(11)]
    public Guid CommandId { get; }

    public NetworkBattleGuardFixtureRoute(
        string battleInstanceId,
        Guid commandId,
        Guid guardAgentId,
        string guardAuthority,
        float startX,
        float startY,
        float startZ,
        float directionX,
        float directionY,
        float length,
        BattleGuardFixturePhase phase)
    {
        BattleInstanceId = battleInstanceId;
        CommandId = commandId;
        GuardAgentId = guardAgentId;
        GuardAuthority = guardAuthority;
        StartX = startX;
        StartY = startY;
        StartZ = startZ;
        DirectionX = directionX;
        DirectionY = directionY;
        Length = length;
        Phase = phase;
    }
}
#endif
