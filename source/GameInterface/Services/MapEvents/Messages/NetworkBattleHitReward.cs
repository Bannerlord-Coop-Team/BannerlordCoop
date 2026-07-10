using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleHitReward : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string AffectedCharacterId;
    [ProtoMember(3)]
    public readonly string AffectorCharacterId;
    [ProtoMember(4)]
    public readonly string CaptainId;
    [ProtoMember(5)]
    public readonly string HeroId;
    [ProtoMember(6)]
    public readonly BattleSideEnum AffectedAgentSide;
    [ProtoMember(7)]
    public readonly BattleSideEnum AffectorAgentSide;
    [ProtoMember(8)]
    public readonly bool IsAgentMounted;
    [ProtoMember(9)]
    public readonly float LastSpeedBonus;
    [ProtoMember(10)]
    public readonly float LastShotDifficulty;
    [ProtoMember(11)]
    public readonly bool IsSiegeEngineHit;
    [ProtoMember(12)]
    public readonly int LastAttackerWeapon; // WeaponComponentData
    [ProtoMember(13)]
    public readonly AgentAttackType AttackType;
    [ProtoMember(14)]
    public readonly float HitpointRatio;
    [ProtoMember(15)]
    public readonly float DamageAmount;
    [ProtoMember(16)]
    public readonly bool IsValidAgent;
    [ProtoMember(17)]
    public readonly string AffectorPartyId;
    [ProtoMember(18)]
    public readonly bool IsSneakAttack;
    [ProtoMember(19)]
    public readonly float AffectedAgentHealth;
    [ProtoMember(20)]
    public readonly bool IsAffectorUnderCommand;

    public NetworkBattleHitReward(
        string mapEventId,
        string affectedCharacterId,
        string affectorCharacterId,
        string captainId,
        string heroId,
        BattleSideEnum affectedAgentSide,
        BattleSideEnum affectorAgentSide,
        bool isAgentMounted,
        float lastSpeedBonus,
        float lastShotDifficulty,
        bool isSiegeEngineHit,
        int lastAttackerWeapon,
        AgentAttackType attackType,
        float hitpointRatio,
        float damageAmount,
        bool isValidAgent,
        string affectorPartyId,
        bool isSneakAttack,
        float affectedAgentHealth,
        bool isAffectorUnderCommand)
    {
        MapEventId = mapEventId;
        AffectedCharacterId = affectedCharacterId;
        AffectorCharacterId = affectorCharacterId;
        CaptainId = captainId;
        HeroId = heroId;
        AffectedAgentSide = affectedAgentSide;
        AffectorAgentSide = affectorAgentSide;
        IsAgentMounted = isAgentMounted;
        LastSpeedBonus = lastSpeedBonus;
        LastShotDifficulty = lastShotDifficulty;
        IsSiegeEngineHit = isSiegeEngineHit;
        LastAttackerWeapon = lastAttackerWeapon;
        AttackType = attackType;
        HitpointRatio = hitpointRatio;
        DamageAmount = damageAmount;
        IsValidAgent = isValidAgent;
        AffectorPartyId = affectorPartyId;
        IsSneakAttack = isSneakAttack;
        AffectedAgentHealth = affectedAgentHealth;
        IsAffectorUnderCommand = isAffectorUnderCommand;
    }
}
