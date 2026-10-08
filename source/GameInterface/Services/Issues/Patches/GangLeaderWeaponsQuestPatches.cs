using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[HarmonyPatch(typeof(Quest))]
internal static class GangLeaderWeaponsQuestPatches
{
    private static bool IsLocalOwner(Quest quest) => ModInformation.IsClient &&
        ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) && ownership.IsLocalPeerOwner(quest.QuestGiver);

    private static bool Request(Quest quest, GangLeaderWeaponsAction action)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            (ModInformation.IsServer && GangLeaderWeaponsActionScope.Contains(quest))) return true;
        if (IsLocalOwner(quest)) MessageBroker.Instance.Publish(quest, new GangLeaderWeaponsActionRequested(quest, action));
        return false;
    }

    [HarmonyPatch("QuestAcceptedConsequences")]
    [HarmonyPrefix]
    private static bool AcceptPrefix(Quest __instance) => __instance._playerStartsQuestLog == null &&
        (QuestSolutionStartAuthorityGuard.IsActive || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate());

    [HarmonyPatch("DeleteAllWeaponsFromPlayer")]
    [HarmonyPrefix]
    private static bool SurrenderPrefix(Quest __instance) => Request(__instance, GangLeaderWeaponsAction.SurrenderWeapons);

    [HarmonyPatch("PlayerBribeGuard")]
    [HarmonyPrefix]
    private static bool BribePrefix(Quest __instance) => Request(__instance, GangLeaderWeaponsAction.Bribe);

    [HarmonyPatch("PlayerDodgeGuardsLowCrimeRating")]
    [HarmonyPrefix]
    private static bool IntimidatePrefix(Quest __instance) => Request(__instance, GangLeaderWeaponsAction.Intimidate);

    [HarmonyPatch("PlayerSuccessfullyDeliveredWeapons")]
    [HarmonyPrefix]
    private static bool DeliverPrefix(Quest __instance) => Request(__instance, GangLeaderWeaponsAction.DeliverWeapons);

    [HarmonyPatch("StartFight")]
    [HarmonyPrefix]
    private static bool StartFightPrefix(Quest __instance) => Request(__instance, GangLeaderWeaponsAction.BeginBattle);

    [HarmonyPatch("OnGameMenuOpened")]
    [HarmonyPrefix]
    private static bool GameMenuPrefix(Quest __instance) =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || IsLocalOwner(__instance);

    private static bool IsAuthoritativeConsequence(Quest quest) =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ModInformation.IsServer && GangLeaderWeaponsActionScope.Contains(quest));

    [HarmonyPatch("PlayerDodgedGuards")]
    [HarmonyPrefix]
    private static bool DodgePrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("PlayerDefeatedAgainstGuards")]
    [HarmonyPrefix]
    private static bool DefeatPrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("CreateGuardsParty")]
    [HarmonyPrefix]
    private static bool CreateGuardsPrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("GiveBackPlayersWeaponsOnCancelOrTimeOut")]
    [HarmonyPrefix]
    private static bool ReturnWeaponsPrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("OnFailed")]
    [HarmonyPrefix]
    private static bool FailedPrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool TimedOutPrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("OnFinalize")]
    [HarmonyPrefix]
    private static bool FinalizePrefix(Quest __instance) => IsAuthoritativeConsequence(__instance);

    [HarmonyPatch("OnSettlementEnter")]
    [HarmonyPrefix]
    private static bool EnterPrefix(Quest __instance, MobileParty party, Settlement settlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!IsLocalOwner(__instance) || __instance._playerDodgedGuards || party != MobileParty.MainParty ||
            settlement != __instance.QuestGiver.CurrentSettlement || party.Army != null ||
            Campaign.Current.GameMenuManager.NextLocation != null || GameStateManager.Current.ActiveState is not MapState ||
            PlayerEncounter.EncounterSettlement == null || __instance._playerGoBack) return false;
        if (AllowedThread.IsThisThreadAllowed()) return __instance._guardsParty != null;
        return Request(__instance, GangLeaderWeaponsAction.EnterTown);
    }

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyPrefix]
    private static bool LeavePrefix(Quest __instance, MobileParty party, Settlement settlement, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            (ModInformation.IsServer && GangLeaderWeaponsActionScope.Contains(__instance))) return true;
        if (ModInformation.IsServer)
        {
            if (settlement != __instance.QuestGiver.CurrentSettlement ||
                !ContainerProvider.TryResolve<IGangLeaderWeaponsOwnerContext>(out var context) ||
                !context.TryOpen(__instance, out __state)) return false;
            return party == MobileParty.MainParty;
        }
        if (party == MobileParty.MainParty && settlement == __instance.QuestGiver.CurrentSettlement)
            Request(__instance, GangLeaderWeaponsAction.LeaveTown);
        return false;
    }

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyPostfix]
    private static void LeavePostfix(Quest __instance, MobileParty party, IDisposable __state)
    {
        if (__state != null && party == MobileParty.MainParty)
            MessageBroker.Instance.Publish(__instance, new GangLeaderWeaponsStateChanged(__instance, GangLeaderWeaponsAction.LeaveTown));
    }

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyFinalizer]
    private static void LeaveFinalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch("OnPlayerInventoryChanged")]
    [HarmonyPrefix]
    private static bool InventoryPrefix(Quest __instance) =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || IsLocalOwner(__instance);

    [HarmonyPatch("OnItemCrafted")]
    [HarmonyPrefix]
    private static bool CraftPrefix(Quest __instance) => InventoryPrefix(__instance);

    [HarmonyPatch("OnEquipmentSmeltedByHero")]
    [HarmonyPrefix]
    private static bool SmeltPrefix(Quest __instance) => InventoryPrefix(__instance);

    [HarmonyPatch("OnPlayerInventoryChanged")]
    [HarmonyPostfix]
    private static void InventoryPostfix(Quest __instance)
    {
        if (IsLocalOwner(__instance)) Request(__instance, GangLeaderWeaponsAction.RefreshProgress);
    }

    [HarmonyPatch("OnItemCrafted")]
    [HarmonyPostfix]
    private static void CraftPostfix(Quest __instance) => InventoryPostfix(__instance);

    [HarmonyPatch("OnEquipmentSmeltedByHero")]
    [HarmonyPostfix]
    private static void SmeltPostfix(Quest __instance) => InventoryPostfix(__instance);

    [HarmonyPatch("persuasion_start_with_guards_on_consequence")]
    [HarmonyPostfix]
    private static void PersuasionStartedPostfix(Quest __instance)
    {
        if (IsLocalOwner(__instance)) Request(__instance, GangLeaderWeaponsAction.BeginPersuasion);
    }

    [HarmonyPatch("persuasion_complete_with_guards_on_consequence")]
    [HarmonyPostfix]
    private static void PersuasionCompletedPostfix(Quest __instance)
    {
        if (IsLocalOwner(__instance)) Request(__instance, GangLeaderWeaponsAction.PersuasionSucceeded);
    }

    [HarmonyPatch("DialogStartCondition")]
    [HarmonyPrefix]
    private static bool OwnedConditionPrefix(Quest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || IsLocalOwner(__instance) ||
            (ModInformation.IsServer && GangLeaderWeaponsActionScope.Contains(__instance))) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch("persuasion_start_with_guards_on_condition")]
    [HarmonyPrefix]
    private static bool PersuasionConditionPrefix(Quest __instance, ref bool __result) =>
        OwnedConditionPrefix(__instance, ref __result);

    [HarmonyPatch("CheckIfPlayerHasEnoughRequestedWeapons")]
    [HarmonyPrefix]
    private static bool DeliveryConditionPrefix(Quest __instance, ref bool __result) =>
        OwnedConditionPrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
internal static class GangLeaderWeaponsAlternativeStartPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => AlternativeSolutionStartAuthorityGuard.IsActive ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}

[HarmonyPatch(typeof(IssueBase), "get_IssueQuestCanBeDuplicated")]
internal static class GangLeaderWeaponsDuplicateEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            Hero.MainHero == null || !PlayerManager.TryGetControlledObjectInfo(Hero.MainHero, out var player) ||
            !ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return true;
        foreach (var entry in Campaign.Current.IssueManager.Issues)
        {
            if (entry.Value is not Issue || (!entry.Value.IsSolvingWithQuest && !entry.Value.IsSolvingWithAlternative)) continue;
            if (!ownership.TryGetOwnerControllerId(entry.Key, out var controllerId) || controllerId == player.ObjectControllerId)
            {
                __result = false;
                return false;
            }
        }
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.OnConversationEnd))]
internal static class GangLeaderWeaponsObserverConversationPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        var quest = GangLeaderWeaponsActionScope.Current;
        if (quest == null || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) && ownership.IsLocalPeerOwner(quest.QuestGiver);
    }
}
