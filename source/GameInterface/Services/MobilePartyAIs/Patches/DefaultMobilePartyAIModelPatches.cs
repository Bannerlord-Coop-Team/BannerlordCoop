using Common;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(DefaultMobilePartyAIModel))]
internal class DefaultMobilePartyAIModelPatches
{
    public static readonly ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>> DisablePlayerAttackTimes = new ConditionalWeakTable<MobilePartyAi, Dictionary<MobileParty, CampaignTime>>();
    private static readonly Dictionary<MobileParty, Dictionary<MobileParty, CampaignTime>> PersistedDisablePlayerAttackTimes = new Dictionary<MobileParty, Dictionary<MobileParty, CampaignTime>>();

    internal static void PreventAttacksUntil(MobileParty attackerParty, MobileParty targetParty, CampaignTime disabledUntil)
    {
        if (attackerParty?.Ai == null || targetParty == null) return;

        DisablePlayerAttackTimes.GetOrCreateValue(attackerParty.Ai)[targetParty] = disabledUntil;

        if (!PersistedDisablePlayerAttackTimes.TryGetValue(attackerParty, out var disableTimes))
        {
            disableTimes = new Dictionary<MobileParty, CampaignTime>();
            PersistedDisablePlayerAttackTimes[attackerParty] = disableTimes;
        }

        disableTimes[targetParty] = disabledUntil;
    }

    internal static IEnumerable<(MobileParty AttackerParty, MobileParty TargetParty, CampaignTime DisabledUntil)> GetPersistedAttackProtections()
    {
        foreach (var attackerEntry in PersistedDisablePlayerAttackTimes)
        {
            foreach (var targetEntry in attackerEntry.Value)
            {
                yield return (attackerEntry.Key, targetEntry.Key, targetEntry.Value);
            }
        }
    }

    internal static void ResetPersistedAttackProtections()
    {
        foreach (var attackerParty in PersistedDisablePlayerAttackTimes.Keys)
        {
            if (attackerParty?.Ai != null)
                DisablePlayerAttackTimes.Remove(attackerParty.Ai);
        }

        PersistedDisablePlayerAttackTimes.Clear();
    }

    internal static void PrunePersistedAttackProtections(CampaignTime currentTime)
    {
        var staleProtections = GetPersistedAttackProtections()
            .Where(protection => protection.AttackerParty?.IsActive != true
                || protection.TargetParty?.IsActive != true
                || currentTime > protection.DisabledUntil)
            .ToList();

        foreach (var protection in staleProtections)
            RemoveAttackProtection(protection.AttackerParty, protection.TargetParty);
    }

    internal static void RemoveAttackProtectionsForParty(MobileParty party)
    {
        if (party == null) return;

        var staleProtections = GetPersistedAttackProtections()
            .Where(protection => protection.AttackerParty == party || protection.TargetParty == party)
            .ToList();

        foreach (var protection in staleProtections)
            RemoveAttackProtection(protection.AttackerParty, protection.TargetParty);
    }

    private static void RemoveAttackProtection(MobileParty attackerParty, MobileParty targetParty)
    {
        if (attackerParty?.Ai != null
            && DisablePlayerAttackTimes.TryGetValue(attackerParty.Ai, out var runtimeDisableTimes))
        {
            runtimeDisableTimes.Remove(targetParty);
            if (runtimeDisableTimes.Count == 0)
                DisablePlayerAttackTimes.Remove(attackerParty.Ai);
        }

        if (!PersistedDisablePlayerAttackTimes.TryGetValue(attackerParty, out var persistedDisableTimes)) return;

        persistedDisableTimes.Remove(targetParty);
        if (persistedDisableTimes.Count == 0)
            PersistedDisablePlayerAttackTimes.Remove(attackerParty);
    }

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
            RemoveAttackProtection(party, targetParty);
            return true;
        }

        return false;
    }
}
