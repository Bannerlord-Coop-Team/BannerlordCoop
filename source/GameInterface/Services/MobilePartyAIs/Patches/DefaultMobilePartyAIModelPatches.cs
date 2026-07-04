using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(DefaultMobilePartyAIModel))]
internal class DefaultMobilePartyAIModelPatches
{
    public static readonly ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>> DisablePlayerAttackTimes = new ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>>();

    [HarmonyPatch(nameof(DefaultMobilePartyAIModel.ShouldConsiderAttacking))]
    [HarmonyPostfix]
    private static void ShouldConsiderAttacking_Postfix(
        MobileParty party,
        MobileParty targetParty,
        ref bool __result)
    {
        if (!__result)
            return;

        // TODO test with player parties
        if (targetParty.ShouldBeIgnored)
            __result = false;

        if (!CanAttackTargetParty(party, targetParty))
            __result = false;

        // Don't consider attacking a party that is held in a conversation with a player; the interaction guard
        // would block the attack anyway, and this keeps the AI from chasing an unattackable target.
        if (ConversationPartyHold.IsInPlayerConversation(targetParty))
            __result = false;
    }
    [HarmonyPatch(nameof(DefaultMobilePartyAIModel.ShouldPartyCheckInitiativeBehavior))]
    [HarmonyPrefix]
    private static bool ShouldPartyCheckInitiativeBehaviorPrefix(MobileParty mobileParty, ref bool __result)
    {
        if (mobileParty.IsPlayerParty())
        {
            __result = false;
            return false;
        }

        return true;
    }
    [HarmonyPatch(typeof(DefaultMobilePartyAIModel))]
    internal class FixGarrisonFleePatch
    {
        [HarmonyPatch("CalculateInitiativeScoresForEnemy")]
        static bool Prefix(MobileParty mobileParty, MobileParty enemyParty, ref float avoidScore, ref float attackScore)
        {
            if (!ModInformation.IsServer) return true;
            if (!enemyParty.IsGarrison) return true;
            if (enemyParty.CurrentSettlement == null) return true;

            avoidScore = 0f;
            attackScore = 0f;
            return false;
        }
    }
    private static bool CanAttackTargetParty(MobileParty party, MobileParty targetParty)
    {
        if (party?.Ai == null || targetParty == null)
            return true;

        if (!DisablePlayerAttackTimes.TryGetValue(party.Ai, out var disableTimes))
            return true;

        if (!disableTimes.TryGetValue(targetParty, out var disableTime))
            return true;

        if (disableTime.IsPast)
        {
            disableTimes.Remove(targetParty);
            return true;
        }

        return false;
    }
}
