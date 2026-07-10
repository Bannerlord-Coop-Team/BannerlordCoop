using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(BattleAgentLogic))]
internal class BattleAgentLogicHitRewardPatch
{
    [HarmonyPatch(nameof(BattleAgentLogic.EnemyHitReward))]
    [HarmonyPrefix]
    public static bool EnemyHitRewardPrefix(BattleAgentLogic __instance, Agent affectedAgent, Agent affectorAgent, float lastSpeedBonus, float lastShotDifficulty, bool isSiegeEngineHit, WeaponComponentData lastAttackerWeapon, AgentAttackType attackType, float hitpointRatio, float damageAmount, bool isSneakAttack)
    {
        if (ModInformation.IsServer) return true;

        Hero hero = (affectorAgent.Team.Leader != null && affectorAgent.Team.Leader.Character.IsHero) ? ((CharacterObject)affectorAgent.Team.Leader.Character).HeroObject : null;
        var message = new BattleHitReward(
            MapEvent.PlayerMapEvent,
            (CharacterObject)affectedAgent.Character,
            (CharacterObject)affectorAgent.Character,
            BattleAgentLogic.GetCaptain(affectorAgent),
            hero,
            affectedAgent.Team.Side,
            affectorAgent.Team.Side,
            affectorAgent.MountAgent != null,
            lastSpeedBonus,
            lastShotDifficulty,
            isSiegeEngineHit,
            lastAttackerWeapon,
            attackType,
            hitpointRatio,
            damageAmount,
            affectedAgent.Origin != null && affectorAgent != null && affectorAgent.Origin != null && affectorAgent.Team != null && affectorAgent.Team.IsValid && affectedAgent.Team != null && affectedAgent.Team.IsValid,
            (PartyBase)affectorAgent.Origin.BattleCombatant,
            isSneakAttack,
            affectedAgent.Health,
            hero != null && affectorAgent.Character != hero.CharacterObject && (hero != Hero.MainHero || affectorAgent.Formation == null || !affectorAgent.Formation.IsAIControlled));
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    /// <summary>
    /// Retain vanilla implementation on client except for updating upgraded troop counts using the server
    /// </summary>
    [HarmonyPatch(nameof(BattleAgentLogic.OnAgentRemoved))]
    [HarmonyPrefix]
    public static bool OnAgentRemovedPrefix(BattleAgentLogic __instance, Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
    {
        if (affectorAgent == null && affectedAgent.IsMount && agentState == AgentState.Routed) return false;

        CharacterObject characterObject = (CharacterObject)affectedAgent.Character;
        CharacterObject characterObject2 = (CharacterObject)((affectorAgent != null) ? affectorAgent.Character : null);
        if (affectedAgent.Origin != null)
        {
            PartyBase partyBase = (PartyBase)affectedAgent.Origin.BattleCombatant;
            if (agentState == AgentState.Unconscious)
            {
                affectedAgent.Origin.SetWounded();
                return false;
            }
            if (agentState == AgentState.Killed)
            {
                affectedAgent.Origin.SetKilled();
                Hero hero = affectedAgent.IsHuman ? characterObject.HeroObject : null;
                Hero hero2 = (affectorAgent == null) ? null : (affectorAgent.IsHuman ? characterObject2.HeroObject : null);
                if (hero != null && hero2 != null)
                {
                    CampaignEventDispatcher.Instance.OnCharacterDefeated(hero2, hero); // TODO
                }
                if (partyBase != null)
                {
                    // Run on server instead and send message after to clients to update scoreboards
                    //__instance.CheckUpgrade(affectedAgent.Team.Side, partyBase, characterObject);
                    var message = new CheckUpgradeAfterAgentRemoved(MapEvent.PlayerMapEvent, partyBase, characterObject, affectedAgent.Team.Side);
                    MessageBroker.Instance.Publish(__instance, message);

                    return false;
                }
            }
            else
            {
                bool flag = affectedAgent.GetMorale() < 0.01f;
                affectedAgent.Origin.SetRouted(!flag);
            }
        }

        return false;
    }
}
