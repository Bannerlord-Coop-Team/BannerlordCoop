using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
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
    private static readonly Dictionary<MobileParty, Dictionary<IFaction, CampaignTime>> DisabledFactionAttackTimes =
        new Dictionary<MobileParty, Dictionary<IFaction, CampaignTime>>();

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

    internal static void PreventFactionAttacksUntil(
        MobileParty attackerParty,
        IFaction targetFaction,
        CampaignTime disabledUntil)
    {
        if (attackerParty == null || targetFaction == null) return;
        if (!DisabledFactionAttackTimes.TryGetValue(attackerParty, out var disableTimes))
        {
            disableTimes = new Dictionary<IFaction, CampaignTime>();
            DisabledFactionAttackTimes[attackerParty] = disableTimes;
        }

        disableTimes[targetFaction] = disabledUntil;
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

    internal static IEnumerable<(MobileParty AttackerParty, IFaction TargetFaction, CampaignTime DisabledUntil)> GetPersistedFactionAttackProtections()
    {
        foreach (var attackerEntry in DisabledFactionAttackTimes)
        {
            foreach (var factionEntry in attackerEntry.Value)
            {
                yield return (attackerEntry.Key, factionEntry.Key, factionEntry.Value);
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
        DisabledFactionAttackTimes.Clear();
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

        foreach (var attackerEntry in DisabledFactionAttackTimes.ToArray())
        {
            foreach (var factionEntry in attackerEntry.Value
                         .Where(entry => entry.Key == null || currentTime > entry.Value)
                         .ToArray())
                attackerEntry.Value.Remove(factionEntry.Key);

            if (attackerEntry.Key?.IsActive != true || attackerEntry.Value.Count == 0)
                DisabledFactionAttackTimes.Remove(attackerEntry.Key);
        }
    }

    internal static void RemoveAttackProtectionsForParty(MobileParty party)
    {
        if (party == null) return;

        var staleProtections = GetPersistedAttackProtections()
            .Where(protection => protection.AttackerParty == party || protection.TargetParty == party)
            .ToList();

        foreach (var protection in staleProtections)
            RemoveAttackProtection(protection.AttackerParty, protection.TargetParty);

        DisabledFactionAttackTimes.Remove(party);
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

    // v1.5 zeroes an AI party's attack scores while a main party stronger than itself is near it. The server has
    // no main party on the map, so the same check runs for active player parties; a parked offline party doesn't count.
    [HarmonyPatch(nameof(DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy))]
    [HarmonyPostfix]
    private static void CalculateInitiativeScoresForEnemy_Postfix(
        MobileParty mobileParty,
        MobileParty enemyParty,
        ref float attackScore)
    {
        if (attackScore <= 0f) return;
        if (!IsStrongerPlayerPartyNearby(mobileParty, enemyParty)) return;

        attackScore = 0f;
    }

    // Searches the parties GetBestInitiativeBehavior checks around the attacker for a stronger player party.
    private static bool IsStrongerPlayerPartyNearby(MobileParty mobileParty, MobileParty enemyParty)
    {
        var searchData = MobileParty.StartFindingLocatablesAroundPosition(
            mobileParty.Position.ToVec2(),
            NearbyPartySearchRadius(mobileParty));

        for (var party = MobileParty.FindNextLocatable(ref searchData);
             party != null;
             party = MobileParty.FindNextLocatable(ref searchData))
        {
            if (IsStrongerPlayerParty(party, mobileParty, enemyParty)) return true;
        }

        return false;
    }

    // The radius GetBestInitiativeBehavior searches around the attacker, wider for sea patrols and port defenders.
    private static float NearbyPartySearchRadius(MobileParty mobileParty)
    {
        var joiningRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        if (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.IsCurrentlyAtSea && mobileParty.IsTargetingPort)
            return joiningRadius * (Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius + 1f);

        if (mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && mobileParty.IsCurrentlyAtSea && !mobileParty.AiBehaviorTarget.IsOnLand)
            return joiningRadius * 5f;

        return joiningRadius * 3f;
    }

    // Vanilla's stronger main party check and the skips it runs before it, read for an active player party.
    private static bool IsStrongerPlayerParty(MobileParty party, MobileParty mobileParty, MobileParty enemyParty)
    {
        if (!party.IsActive || !party.IsPlayerParty()) return false;
        if (party.BesiegedSettlement != null && party.MapFaction == mobileParty.MapFaction) return false;
        if (party.MapEvent != null && party.MapEvent != enemyParty.MapEvent) return false;
        if (party.AttachedTo != null || party.IsInNavalAutoTravel) return false;
        if (party.IsCurrentlyAtSea != mobileParty.IsCurrentlyAtSea) return false;
        if (party.CurrentSettlement?.SiegeEvent != null) return false;

        return party.Party.EstimatedStrength > mobileParty.Party.EstimatedStrength;
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
    internal static bool IsAttackPrevented(MobileParty party, MobileParty targetParty)
        => !CanAttackTargetParty(party, targetParty);

    private static bool CanAttackTargetParty(MobileParty party, MobileParty targetParty)
    {
        if (party == null || targetParty == null)
            return true;

        if (party.Ai != null &&
            DisablePlayerAttackTimes.TryGetValue(party.Ai, out var disableTimes) &&
            disableTimes.TryGetValue(targetParty, out var disableTime))
        {
            if (!disableTime.IsPast)
                return false;

            RemoveAttackProtection(party, targetParty);
        }

        if (!DisabledFactionAttackTimes.TryGetValue(party, out var factionDisableTimes) ||
            targetParty.MapFaction == null ||
            !factionDisableTimes.TryGetValue(targetParty.MapFaction, out var factionDisableTime))
        {
            return true;
        }

        if (!factionDisableTime.IsPast)
            return false;

        factionDisableTimes.Remove(targetParty.MapFaction);
        if (factionDisableTimes.Count == 0)
            DisabledFactionAttackTimes.Remove(party);
        return true;
    }
}
