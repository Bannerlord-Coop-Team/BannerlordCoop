using GameInterface.Services.Kingdoms;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(KingdomDecisionsVM))]
    internal class KingdomDecisionsVMPatches
    {
        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleDecision))]
        [HarmonyPrefix]
        private static bool HandleDecisionPrefix(KingdomDecisionsVM __instance, KingdomDecision __0)
        {
            if (!KingdomDecisionVoteManager.ShouldSuppressLocalDecision(__0)) return true;

            __instance._examinedDecisionsSinceInit.Add(__0);
            return false;
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleDecision))]
        [HarmonyPostfix]
        private static void HandleDecisionPostfix(KingdomDecisionsVM __instance)
        {
            KingdomDecisionVoteManager.RegisterDecisionItem(__instance.CurrentDecision);
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.RefreshWith))]
        [HarmonyPostfix]
        private static void RefreshWithPostfix(KingdomDecisionsVM __instance)
        {
            KingdomDecisionVoteManager.RegisterDecisionItem(__instance.CurrentDecision);
        }
    }

    [HarmonyPatch(typeof(DecisionItemBaseVM))]
    internal class DecisionItemBaseVMPatches
    {
        [HarmonyPatch("OnChangeVote")]
        [HarmonyPostfix]
        private static void OnChangeVotePostfix(DecisionOptionVM __0)
        {
            KingdomDecisionVoteManager.TryPublishVote(__0);
        }

        [HarmonyPatch(nameof(DecisionItemBaseVM.ExecuteFinalSelection))]
        [HarmonyPrefix]
        private static bool ExecuteFinalSelectionPrefix(DecisionItemBaseVM __instance)
        {
            if (!KingdomDecisionVoteManager.ShouldBlockLocalResolution(__instance)) return true;

            KingdomDecisionVoteManager.TryPublishFinalVote(__instance);
            return false;
        }

        [HarmonyPatch("OnFinalize")]
        [HarmonyPostfix]
        private static void OnFinalizePostfix(DecisionItemBaseVM __instance)
        {
            KingdomDecisionVoteManager.UnregisterDecisionItem(__instance);
        }
    }

    [HarmonyPatch(typeof(DecisionOptionVM))]
    internal class DecisionOptionVMPatches
    {
        [HarmonyPatch("OnSupportStrengthChange")]
        [HarmonyPostfix]
        private static void OnSupportStrengthChangePostfix(DecisionOptionVM __instance)
        {
            KingdomDecisionVoteManager.TryPublishVote(__instance);
        }
    }

    [HarmonyPatch(typeof(KingdomPoliciesVM))]
    internal class KingdomPoliciesVMPatches
    {
        [HarmonyPatch(nameof(KingdomPoliciesVM.RefreshValues))]
        [HarmonyPostfix]
        internal static void RefreshValuesPostfix(KingdomPoliciesVM __instance)
        {
            DisablePolicyResolveIfAlreadyVoted(__instance);
        }

        [HarmonyPatch("OnPolicySelect")]
        [HarmonyPostfix]
        internal static void OnPolicySelectPostfix(KingdomPoliciesVM __instance)
        {
            DisablePolicyResolveIfAlreadyVoted(__instance);
        }

        [HarmonyPatch("ExecuteProposeOrDisavow")]
        [HarmonyPrefix]
        internal static bool ExecuteProposeOrDisavowPrefix(KingdomPoliciesVM __instance)
        {
            return !KingdomDecisionVoteManager.ShouldDisableResolveDecision(__instance?._currentItemsUnresolvedDecision);
        }

        internal static void DisablePolicyResolveIfAlreadyVoted(KingdomPoliciesVM policiesVm)
        {
            if (policiesVm == null) return;
            if (!KingdomDecisionVoteManager.ShouldDisableResolveDecision(policiesVm._currentItemsUnresolvedDecision)) return;

            policiesVm.CanProposeOrDisavowPolicy = false;
            if (policiesVm.DoneHint != null)
            {
                policiesVm.DoneHint.HintText = KingdomTabResolveDecisionPatches.AlreadyVotedHint;
            }
        }
    }

    [HarmonyPatch(typeof(KingdomDiplomacyVM))]
    internal class KingdomDiplomacyVMPatches
    {
        [HarmonyPatch(nameof(KingdomDiplomacyVM.RefreshValues))]
        [HarmonyPostfix]
        internal static void RefreshValuesPostfix(KingdomDiplomacyVM __instance)
        {
            DisableDiplomacyResolveActionsIfAlreadyVoted(__instance, __instance.CurrentSelectedDiplomacyItem);
        }

        [HarmonyPatch("OnSetWarItem")]
        [HarmonyPostfix]
        internal static void OnSetWarItemPostfix(KingdomDiplomacyVM __instance, KingdomWarItemVM item)
        {
            DisableDiplomacyResolveActionsIfAlreadyVoted(__instance, item);
        }

        [HarmonyPatch("OnSetPeaceItem")]
        [HarmonyPostfix]
        internal static void OnSetPeaceItemPostfix(KingdomDiplomacyVM __instance, KingdomTruceItemVM item)
        {
            DisableDiplomacyResolveActionsIfAlreadyVoted(__instance, item);
        }

        internal static void DisableDiplomacyResolveActionsIfAlreadyVoted(
            KingdomDiplomacyVM diplomacyVm,
            KingdomDiplomacyItemVM diplomacyItem)
        {
            if (diplomacyVm?.Actions == null || diplomacyItem == null) return;

            List<KingdomDecision> resolveDecisions = GetResolveDecisions(diplomacyItem)
                .Where(decision => decision != null)
                .ToList();
            if (resolveDecisions.Count == 0) return;

            int resolveDecisionIndex = 0;
            foreach (KingdomDiplomacyProposalActionItemVM action in diplomacyVm.Actions)
            {
                if (!KingdomTabResolveDecisionPatches.IsResolveAction(action)) continue;
                if (resolveDecisionIndex >= resolveDecisions.Count) return;

                KingdomDecision resolveDecision = resolveDecisions[resolveDecisionIndex++];
                if (!KingdomDecisionVoteManager.ShouldDisableResolveDecision(resolveDecision)) continue;

                KingdomTabResolveDecisionPatches.DisableAction(action);
            }
        }

        private static IEnumerable<KingdomDecision> GetResolveDecisions(KingdomDiplomacyItemVM diplomacyItem)
        {
            if (Clan.PlayerClan?.Kingdom?.UnresolvedDecisions == null) yield break;

            IFaction faction = diplomacyItem.Faction2;
            if (diplomacyItem is KingdomWarItemVM)
            {
                yield return Clan.PlayerClan.Kingdom.UnresolvedDecisions
                    .OfType<MakePeaceKingdomDecision>()
                    .FirstOrDefault(decision => decision.FactionToMakePeaceWith == faction);
                yield break;
            }

            if (diplomacyItem is not KingdomTruceItemVM) yield break;

            yield return Clan.PlayerClan.Kingdom.UnresolvedDecisions
                .OfType<StartAllianceDecision>()
                .FirstOrDefault(decision => decision.KingdomToStartAllianceWith == faction);
            yield return Clan.PlayerClan.Kingdom.UnresolvedDecisions
                .OfType<DeclareWarDecision>()
                .FirstOrDefault(decision => decision.FactionToDeclareWarOn == faction);
            yield return Clan.PlayerClan.Kingdom.UnresolvedDecisions
                .OfType<TradeAgreementDecision>()
                .FirstOrDefault(decision => decision.TargetKingdom == faction);
        }
    }

    [HarmonyPatch(typeof(KingdomDiplomacyProposalActionItemVM))]
    internal class KingdomDiplomacyProposalActionItemVMPatches
    {
        [HarmonyPatch(nameof(KingdomDiplomacyProposalActionItemVM.ExecuteAction))]
        [HarmonyPrefix]
        internal static bool ExecuteActionPrefix(KingdomDiplomacyProposalActionItemVM __instance)
        {
            return __instance?.IsEnabled ?? false;
        }
    }

    internal static class KingdomTabResolveDecisionPatches
    {
        private static readonly TextObject AlreadyVotedHintText = new TextObject("You have already voted on this decision.");

        internal static TextObject AlreadyVotedHint => AlreadyVotedHintText;

        internal static bool IsResolveAction(KingdomDiplomacyProposalActionItemVM action)
        {
            if (action == null) return false;

            string resolveText = GameTexts.FindText("str_resolve")?.ToString();
            return !string.IsNullOrWhiteSpace(resolveText) && string.Equals(action.Name, resolveText, StringComparison.Ordinal);
        }

        internal static void DisableAction(KingdomDiplomacyProposalActionItemVM action)
        {
            if (action == null) return;

            action.IsEnabled = false;
            if (action.Hint != null)
            {
                action.Hint.HintText = AlreadyVotedHint;
            }
        }
    }
}
