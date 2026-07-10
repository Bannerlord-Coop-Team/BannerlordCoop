using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct BattleHitReward : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly CharacterObject AffectedCharacter;
    public readonly CharacterObject AffectorCharacter;
    public readonly Hero Captain;
    public readonly Hero Hero;
    public readonly BattleSideEnum AffectedAgentSide;
    public readonly BattleSideEnum AffectorAgentSide;
    public readonly bool IsAgentMounted;
    public readonly float LastSpeedBonus;
    public readonly float LastShotDifficulty;
    public readonly bool IsSiegeEngineHit;
    public readonly WeaponComponentData LastAttackerWeapon;
    public readonly AgentAttackType AttackType;
    public readonly float HitpointRatio;
    public readonly float DamageAmount;
    public readonly bool IsValidAgent;
    public readonly PartyBase AffectorParty;
    public readonly bool IsSneakAttack;
    public readonly float AffectedAgentHealth;
    public readonly bool IsAffectorUnderCommand;

    public BattleHitReward(
        MapEvent mapEvent,
        CharacterObject affectedCharacter,
        CharacterObject affectorCharacter,
        Hero captain,
        Hero hero,
        BattleSideEnum affectedAgentSide,
        BattleSideEnum affectorAgentSide,
        bool isAgentMounted,
        float lastSpeedBonus,
        float lastShotDifficulty,
        bool isSiegeEngineHit,
        WeaponComponentData lastAttackerWeapon,
        AgentAttackType attackType,
        float hitpointRatio,
        float damageAmount,
        bool isValidAgent,
        PartyBase affectorParty,
        bool isSneakAttack,
        float affectedAgentHealth,
        bool isAffectorUnderCommand)
    {
        MapEvent = mapEvent;
        AffectedCharacter = affectedCharacter;
        AffectorCharacter = affectorCharacter;
        Captain = captain;
        Hero = hero;
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
        AffectorParty = affectorParty;
        IsSneakAttack = isSneakAttack;
        AffectedAgentHealth = affectedAgentHealth;
        IsAffectorUnderCommand = isAffectorUnderCommand;
    }
}
