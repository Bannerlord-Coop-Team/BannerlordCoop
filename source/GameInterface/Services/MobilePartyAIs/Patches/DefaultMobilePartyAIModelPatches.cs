using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;
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

    // Vanilla zeroes an AI party's attack scores while a main party stronger than itself is near it. The server has
    // no main party on the map, so the same check runs for active player parties; a parked offline party doesn't count.
    [HarmonyPatch(nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> GetBestInitiativeBehavior_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);
        var scoreEnemy = AccessTools.Method(typeof(DefaultMobilePartyAIModel),
            nameof(DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy));
        var scoreWithNearbyPlayers = AccessTools.Method(typeof(DefaultMobilePartyAIModelPatches),
            nameof(ScoreEnemyWithNearbyPlayers));
        int callIndex = codes.FindIndex(code => code.Calls(scoreEnemy));
        if (callIndex < 0 || callIndex != codes.FindLastIndex(code => code.Calls(scoreEnemy)))
            throw new InvalidOperationException("Expected one enemy scoring call in GetBestInitiativeBehavior.");

        // Each attacker decision starts with a default search that runs only for a positive attack score.
        var nearbyPlayers = generator.DeclareLocal(typeof(NearbyPlayerSearch));
        var loadNearbyPlayers = new CodeInstruction(OpCodes.Ldloca, nearbyPlayers);
        codes[callIndex].MoveLabelsTo(loadNearbyPlayers).MoveBlocksTo(loadNearbyPlayers);
        codes[callIndex] = new CodeInstruction(OpCodes.Call, scoreWithNearbyPlayers);
        codes.Insert(callIndex, loadNearbyPlayers);
        return codes;
    }

    // Mirrors the CalculateInitiativeScoresForEnemy stack with the model first and the injected search last.
    private static void ScoreEnemyWithNearbyPlayers(
        DefaultMobilePartyAIModel model,
        MobileParty mobileParty,
        MobileParty enemyParty,
        out float avoidScore,
        out float attackScore,
        float localAdvantage,
        float maxAggressiveness,
        ref NearbyPlayerSearch nearbyPlayers)
    {
        model.CalculateInitiativeScoresForEnemy(mobileParty, enemyParty, out avoidScore, out attackScore, localAdvantage, maxAggressiveness);
        attackScore = SuppressAttackNearStrongerPlayer(mobileParty, enemyParty, attackScore, ref nearbyPlayers);
    }

    internal static float SuppressAttackNearStrongerPlayer(
        MobileParty mobileParty,
        MobileParty enemyParty,
        float attackScore,
        ref NearbyPlayerSearch nearbyPlayers)
    {
        if (attackScore <= 0f) return attackScore;

        return nearbyPlayers.HasStrongerPlayer(mobileParty, enemyParty) ? 0f : attackScore;
    }

    /// <summary>Resumes one attacker's locator scan only when the players already seen cannot suppress an enemy.</summary>
    internal struct NearbyPlayerSearch
    {
        private enum ScanState { NotStarted, InProgress, Exhausted }

        private LocatableSearchData<MobileParty> searchData;
        private ScanState state;
        private List<MobileParty> seenPlayers;

        public bool HasStrongerPlayer(MobileParty attacker, MobileParty enemy) =>
            AnySeenPlayerStronger(attacker, enemy) || ScanForStrongerPlayer(attacker, enemy);

        // Recheck eligibility for this enemy, which may be in a different map event.
        private bool AnySeenPlayerStronger(MobileParty attacker, MobileParty enemy)
        {
            if (seenPlayers == null) return false;

            foreach (var player in seenPlayers)
            {
                if (IsStrongerPlayerParty(player, attacker, enemy))
                    return true;
            }

            return false;
        }

        // Keep the cursor at the first match so a later enemy can continue from there.
        private bool ScanForStrongerPlayer(MobileParty attacker, MobileParty enemy)
        {
            if (state == ScanState.Exhausted) return false;

            if (state == ScanState.NotStarted)
            {
                searchData = MobileParty.StartFindingLocatablesAroundPosition(
                    attacker.Position.ToVec2(), NearbyPartySearchRadius(attacker));
                state = ScanState.InProgress;
            }

            for (var party = MobileParty.FindNextLocatable(ref searchData);
                 party != null;
                 party = MobileParty.FindNextLocatable(ref searchData))
            {
                if (!party.IsActive || !party.IsPlayerParty()) continue;
                (seenPlayers ??= new List<MobileParty>()).Add(party);

                if (IsStrongerPlayerParty(party, attacker, enemy))
                    return true;
            }

            state = ScanState.Exhausted;
            return false;
        }
    }

    // The radius GetBestInitiativeBehavior searches around the attacker, wider for sea patrols and port defenders.
    private static float NearbyPartySearchRadius(MobileParty mobileParty)
    {
        const float SeaPatrolRadiusMultiplier = 5f;
        const float DefaultRadiusMultiplier = 3f;

        var joiningRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;

        bool isPortDefenderAtSea = mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.IsCurrentlyAtSea && mobileParty.IsTargetingPort;
        if (isPortDefenderAtSea)
            return joiningRadius * (Campaign.Current.Models.MobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius + 1f);

        bool isSeaPatrol = mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && mobileParty.IsCurrentlyAtSea && !mobileParty.AiBehaviorTarget.IsOnLand;
        if (isSeaPatrol)
            return joiningRadius * SeaPatrolRadiusMultiplier;

        return joiningRadius * DefaultRadiusMultiplier;
    }

    // Applies vanilla's strength and eligibility checks to an active player party already filtered by the scan.
    private static bool IsStrongerPlayerParty(MobileParty party, MobileParty mobileParty, MobileParty enemyParty)
    {
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

        // A player battle closed to AI joins is ignored, otherwise the attacker walks up and gets held beside it.
        if (InteractionPatches.IsClosedToAiJoins(targetParty.MapEvent))
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
