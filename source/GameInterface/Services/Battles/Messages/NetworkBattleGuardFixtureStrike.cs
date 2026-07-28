#if DEBUG
using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Battles.Messages;

/// <summary>Shares collision authority's mounted strike path with the guard owner.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleGuardFixtureStrike : IEvent
{
    [ProtoMember(1)]
    public string BattleInstanceId { get; }

    [ProtoMember(2)]
    public Guid CommandId { get; }

    [ProtoMember(3)]
    public Guid GuardAgentId { get; }

    [ProtoMember(4)]
    public string GuardAuthority { get; }

    [ProtoMember(5)]
    public Guid StrikerAgentId { get; }

    [ProtoMember(6)]
    public string StrikerAuthority { get; }

    [ProtoMember(7)]
    public bool Active { get; }

    [ProtoMember(8)]
    public float TravelDirectionX { get; }

    [ProtoMember(9)]
    public float TravelDirectionY { get; }

    [ProtoMember(10)]
    public float GuardLookDirectionX { get; }

    [ProtoMember(11)]
    public float GuardLookDirectionY { get; }

    [ProtoMember(12)]
    public float TargetX { get; }

    [ProtoMember(13)]
    public float TargetY { get; }

    public NetworkBattleGuardFixtureStrike(
        string battleInstanceId,
        Guid commandId,
        Guid guardAgentId,
        string guardAuthority,
        Guid strikerAgentId,
        string strikerAuthority,
        bool active,
        float travelDirectionX,
        float travelDirectionY,
        float guardLookDirectionX,
        float guardLookDirectionY,
        float targetX,
        float targetY)
    {
        BattleInstanceId = battleInstanceId;
        CommandId = commandId;
        GuardAgentId = guardAgentId;
        GuardAuthority = guardAuthority;
        StrikerAgentId = strikerAgentId;
        StrikerAuthority = strikerAuthority;
        Active = active;
        TravelDirectionX = travelDirectionX;
        TravelDirectionY = travelDirectionY;
        GuardLookDirectionX = guardLookDirectionX;
        GuardLookDirectionY = guardLookDirectionY;
        TargetX = targetX;
        TargetY = targetY;
    }
}
#endif
