using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentHitProgressionData
{
    [ProtoMember(1)] public readonly string SessionId;
    [ProtoMember(2)] public readonly string MatchId;
    [ProtoMember(3)] public readonly long Revision;
    [ProtoMember(4)] public readonly long BracketRevision;
    [ProtoMember(5)] public readonly string DamageOriginControllerId;
    [ProtoMember(6)] public readonly long DamageSequence;
    [ProtoMember(7)] public readonly Guid AttackerAgentId;
    [ProtoMember(8)] public readonly Guid VictimAgentId;
    [ProtoMember(9)] public readonly string WeaponItemId;
    [ProtoMember(10)] public readonly int WeaponUsageIndex;
    [ProtoMember(11)] public readonly float MovementSpeedModifier;
    [ProtoMember(12)] public readonly float ShotDifficulty;
    [ProtoMember(13)] public readonly float HitpointRatio;
    [ProtoMember(14)] public readonly float DamageAmount;
    [ProtoMember(15)] public readonly int AttackType;
    [ProtoMember(16)] public readonly bool AttackerMounted;
    [ProtoMember(17)] public readonly bool SameTeam;
    [ProtoMember(18)] public readonly bool Fatal;
    [ProtoMember(19)] public readonly bool Charging;
    [ProtoMember(20)] public readonly bool SneakAttack;

    public TournamentHitProgressionData(
        string sessionId,
        string matchId,
        long revision,
        long bracketRevision,
        string damageOriginControllerId,
        long damageSequence,
        Guid attackerAgentId,
        Guid victimAgentId,
        string weaponItemId,
        int weaponUsageIndex,
        float movementSpeedModifier,
        float shotDifficulty,
        float hitpointRatio,
        float damageAmount,
        int attackType,
        bool attackerMounted,
        bool sameTeam,
        bool fatal,
        bool charging,
        bool sneakAttack)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        BracketRevision = bracketRevision;
        DamageOriginControllerId = damageOriginControllerId;
        DamageSequence = damageSequence;
        AttackerAgentId = attackerAgentId;
        VictimAgentId = victimAgentId;
        WeaponItemId = weaponItemId;
        WeaponUsageIndex = weaponUsageIndex;
        MovementSpeedModifier = movementSpeedModifier;
        ShotDifficulty = shotDifficulty;
        HitpointRatio = hitpointRatio;
        DamageAmount = damageAmount;
        AttackType = attackType;
        AttackerMounted = attackerMounted;
        SameTeam = sameTeam;
        Fatal = fatal;
        Charging = charging;
        SneakAttack = sneakAttack;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSubmitTournamentHitProgression : ICommand
{
    [ProtoMember(1)] public readonly TournamentHitProgressionData Data;

    public NetworkSubmitTournamentHitProgression(TournamentHitProgressionData data)
    {
        Data = data;
    }
}
