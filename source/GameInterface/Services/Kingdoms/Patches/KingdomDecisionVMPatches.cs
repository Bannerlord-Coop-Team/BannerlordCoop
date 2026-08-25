using GameInterface.Services.Kingdoms.Extentions;
using Common.Logging;
using GameInterface.Services.Clans.Handlers;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(KingdomDecisionsVM))]
    internal class KingdomDecisionsVMPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<VassalServiceHandler>();
        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleDecision))]
        [HarmonyPrefix]
        private static bool HandleDecisionPrefix(KingdomDecisionsVM __instance, KingdomDecision __0)
        {
            if (!TryGetVoteManager(out var voteManager)) return true;
            if (!voteManager.ShouldSuppressLocalDecision(__0)) return true;

            __instance._examinedDecisionsSinceInit.Add(__0);
            return false;
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleDecision))]
        [HarmonyPostfix]
        private static void HandleDecisionPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;

            voteManager.RegisterDecisionItem(__instance.CurrentDecision);
            KingdomDecisionWaitingStatusWidgetPatch.EnsureAttached(__instance);
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.RefreshWith))]
        [HarmonyPostfix]
        private static void RefreshWithPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;

            voteManager.RegisterDecisionItem(__instance.CurrentDecision);
            KingdomDecisionWaitingStatusWidgetPatch.EnsureAttached(__instance);
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.OnFrameTick))]
        [HarmonyPostfix]
        private static void OnFrameTickPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;
            DecisionItemBaseVM currentDecision = __instance.CurrentDecision;
            if (currentDecision == null) return;

            voteManager.RefreshDecisionTitle(currentDecision);
            string feedback = voteManager.RefreshDecisionWaitingStatus(currentDecision);
            IReadOnlyList<string> columns = voteManager.GetDecisionWaitingColumns(currentDecision);
            KingdomDecisionWaitingStatusWidgetPatch.EnsureAttached(__instance);
            KingdomDecisionWaitingStatusWidgetPatch.Refresh(__instance, feedback, columns);
        }

        internal static bool TryGetVoteManager(out IKingdomDecisionVoteManager voteManager)
        {
            return ContainerProvider.TryResolve(out voteManager);
        }

        // Bypass vanilla's single-clan shortcut so the receiving player gets the normal peace decision UI and vote.
        [HarmonyPatch(typeof(KingdomDecisionsVM), nameof(KingdomDecisionsVM.RefreshWith))]
        [HarmonyPrefix]
        internal static bool RefreshWithPrefix(KingdomDecisionsVM __instance, KingdomDecision decision)
        {
            if ((CoopKingdomElection.IsPendingPlayerPeaceOffer(decision) || CoopKingdomElection.IsPendingPlayerAllianceOffer(decision)) && decision.IsSingleClanDecision())
            {
                __instance._shouldCheckForDecision = false;
                DecisionItemBaseVM decisionItem = __instance.GetDecisionItemBasedOnType(decision);

                __instance.CurrentDecision = decisionItem;
                __instance.CurrentDecision.SetDoneInputKey(__instance.DoneInputKey);
                return false;
            }

            // KingdomManagementVM.ForceDecideDecision reaches RefreshWith without going through HandleDecision,
            // so gate a suppressed decision here too, otherwise it opens a screen the local clan cant vote on.
            return !TryGetVoteManager(out var voteManager) || !voteManager.ShouldSuppressLocalDecision(decision);
        }
    }

    [HarmonyPatch(typeof(DecisionItemBaseVM))]
    internal class DecisionItemBaseVMPatches
    {
        [HarmonyPatch("OnChangeVote")]
        [HarmonyPostfix]
        private static void OnChangeVotePostfix(DecisionOptionVM __0)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;

            voteManager.TryPublishVote(__0);
        }

        [HarmonyPatch(nameof(DecisionItemBaseVM.ExecuteFinalSelection))]
        [HarmonyPrefix]
        internal static bool ExecuteFinalSelectionPrefix(DecisionItemBaseVM __instance)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return true;

            if (voteManager.IsLocalPlayerEligible(__instance) && voteManager.TryPublishFinalVote(__instance))
            {
                return false;
            }

            // native resolution applies an AI outcome locally for a clan that cant vote and records an abstain
            // nobody picked when the vote wont route, so close the screen instead of falling through to it
            voteManager.CloseDecisionItem(__instance);
            return false;
        }

        [HarmonyPatch("OnFinalize")]
        [HarmonyPostfix]
        private static void OnFinalizePostfix(DecisionItemBaseVM __instance)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;

            voteManager.UnregisterDecisionItem(__instance);
        }

        [HarmonyPatch("RefreshWinPercentages")]
        [HarmonyPrefix]
        private static bool RefreshWinPercentagesPrefix(DecisionItemBaseVM __instance)
        {
            if (__instance?.DecisionOptionsList == null || __instance.KingdomDecisionMaker == null) return true;
            if (__instance.DecisionOptionsList.Any(option => option.Sponsor != null)) return true;

            __instance.KingdomDecisionMaker.DetermineOfficialSupport();

            List<DecisionOptionVM> decisionOptions = __instance.DecisionOptionsList
                .Where(option => !option.IsOptionForAbstain && option.Option != null)
                .ToList();
            if (decisionOptions.Count == 0) return false;

            foreach (DecisionOptionVM decisionOption in decisionOptions)
            {
                decisionOption.WinPercentage = (int)TaleWorlds.Library.MathF.Round(
                    decisionOption.Option.WinChance * 100f,
                    2);
            }

            int assignedPercentage = decisionOptions.Sum(option => option.WinPercentage);
            int remainingPercentage = 100 - assignedPercentage;
            if (remainingPercentage == 0) return false;

            if (assignedPercentage == 0)
            {
                int evenPercentage = 100 / decisionOptions.Count;
                foreach (DecisionOptionVM decisionOption in decisionOptions)
                {
                    decisionOption.WinPercentage = evenPercentage;
                }

                remainingPercentage = 100 - (evenPercentage * decisionOptions.Count);
                decisionOptions[0].WinPercentage += remainingPercentage;
                return false;
            }

            int distributedPercentage = 0;
            foreach (DecisionOptionVM decisionOption in decisionOptions.Where(option => option.WinPercentage > 0))
            {
                int adjustment = TaleWorlds.Library.MathF.Floor(
                    (float)remainingPercentage * decisionOption.WinPercentage / assignedPercentage);
                decisionOption.WinPercentage += adjustment;
                distributedPercentage += adjustment;
            }

            DecisionOptionVM strongestOption = decisionOptions
                .OrderByDescending(option => option.WinPercentage)
                .First();
            strongestOption.WinPercentage += remainingPercentage - distributedPercentage;
            return false;
        }

        [HarmonyPatch("InitValues")]
        [HarmonyPostfix]
        private static void InitValuesPostfix(DecisionItemBaseVM __instance)
        {
            bool isLocalPlayerChooser = __instance.KingdomDecisionMaker._chooser == Clan.PlayerClan;

            __instance.CurrentStageIndex = isLocalPlayerChooser ? 1 : 0;
            __instance.IsPlayerSupporter = !isLocalPlayerChooser;

            __instance.TitleText = isLocalPlayerChooser
                ? __instance.KingdomDecisionMaker._decision.GetChooseTitle().ToString()
                : __instance.KingdomDecisionMaker._decision.GetSupportTitle().ToString();
            __instance.DescriptionText = isLocalPlayerChooser
                ? __instance.KingdomDecisionMaker._decision.GetChooseDescription().ToString()
                : __instance.KingdomDecisionMaker._decision.GetSupportDescription().ToString();
        }
    }

    [HarmonyPatch(typeof(DecisionOptionVM))]
    internal class DecisionOptionVMPatches
    {
        [HarmonyPatch("OnSupportStrengthChange")]
        [HarmonyPostfix]
        private static void OnSupportStrengthChangePostfix(DecisionOptionVM __instance)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;

            voteManager.TryPublishVote(__instance);
        }
        [HarmonyPatch(MethodType.Constructor, typeof(DecisionOutcome), typeof(KingdomDecision), typeof(KingdomElection), typeof(Action<DecisionOptionVM>), typeof(Action<DecisionOptionVM>))]
        [HarmonyPostfix]
        private static void Postfix(DecisionOptionVM __instance, KingdomElection kingdomDecisionMaker)
        {
            __instance.IsPlayerSupporter = kingdomDecisionMaker._chooser != Clan.PlayerClan;
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
            return !KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager) ||
                   !voteManager.ShouldDisableResolveDecision(__instance?._currentItemsUnresolvedDecision);
        }

        internal static void DisablePolicyResolveIfAlreadyVoted(KingdomPoliciesVM policiesVm)
        {
            if (policiesVm == null) return;
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;
            if (!voteManager.ShouldDisableResolveDecision(policiesVm._currentItemsUnresolvedDecision)) return;

            policiesVm.CanProposeOrDisavowPolicy = false;
            if (policiesVm.DoneHint != null)
            {
                policiesVm.DoneHint.HintText = KingdomTabResolveDecisionPatches.AlreadyVotedHint;
            }
        }
    }

    [HarmonyPatch(typeof(KingdomDiplomacyVM))]
    public class KingdomDiplomacyVMPatches
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
            if (PeaceOfferIsPending(__instance, item)) return;
            DisableDiplomacyResolveActionsIfAlreadyVoted(__instance, item);
        }

        [HarmonyPatch("OnSetPeaceItem")]
        [HarmonyPostfix]
        internal static void OnSetPeaceItemPostfix(KingdomDiplomacyVM __instance, KingdomTruceItemVM item)
        {
            if (AllianceOfferPending(__instance, item)) return;
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
                if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;
                if (!voteManager.ShouldDisableResolveDecision(resolveDecision)) continue;

                KingdomTabResolveDecisionPatches.DisableAction(action);
            }
        }

        internal static bool PeaceOfferIsPending(KingdomDiplomacyVM diplomacyVm, KingdomWarItemVM diplomacyItem)
        {
            if (diplomacyVm?.Actions == null || diplomacyItem == null) return false;
            if (Clan.PlayerClan?.Kingdom == null) return false;

            Kingdom playerKingdom = Clan.PlayerClan.Kingdom;
            Kingdom targetKingdom = diplomacyItem.Faction2 as Kingdom;
            if (targetKingdom == null) return false;

            return PeaceOfferPendingRegistry.IsPending(playerKingdom.StringId, targetKingdom.StringId);
        }

        internal static bool AllianceOfferPending(KingdomDiplomacyVM diplomacyVm, KingdomTruceItemVM diplomacyItem)
        {
            if (diplomacyVm?.Actions == null || diplomacyItem == null) return false;
            if (Clan.PlayerClan?.Kingdom == null) return false;

            Kingdom playerKingdom = Clan.PlayerClan.Kingdom;
            Kingdom targetKingdom = diplomacyItem.Faction2 as Kingdom;
            if (targetKingdom == null) return false;

            return AllianceOfferPendingRegistry.IsPending(playerKingdom.StringId, targetKingdom.StringId);
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

        /// <summary>
        /// Kingdom.UnresolveDecisions is not uniformly synchronized,
        /// so we have to send a request to the server,
        /// to ask if there is a peace offer in the enemy's unresolved decisions.
        [HarmonyPatch(nameof(KingdomDiplomacyVM.GetIsProposingPeaceEnabledWithReason))]
        [HarmonyPrefix]
        private static bool GetIsProposingPeaceEnabledWithReasonPrefix(
        KingdomDiplomacyVM __instance,
        KingdomWarItemVM item,
        float actionInfluenceCost,
        ref TextObject disabledReason,
        ref bool __result)
        {
            if (item == null || Clan.PlayerClan?.Kingdom == null)
                return true;

            Kingdom playerKingdom = Clan.PlayerClan.Kingdom;
            Kingdom targetKingdom = item.Faction2 as Kingdom;

            if (targetKingdom == null)
                return true;

            if (!playerKingdom._unresolvedDecisions.OfType<MakePeaceKingdomDecision>().Any(d => d.Kingdom == playerKingdom && d.FactionToMakePeaceWith == targetKingdom)
                && !PeaceOfferPendingRegistry.IsPending(playerKingdom.StringId, targetKingdom.StringId))
            {
                return true;
            }
            __result = false;
            disabledReason = new TextObject("You have already offered peace to this kingdom.");
            return false;
        }

        [HarmonyPatch(nameof(KingdomDiplomacyVM.GetIsProposingAllianceEnabledWithReason))]
        [HarmonyPrefix]
        private static bool GetIsProposingAllianceEnabledWithReasonPrefix(
        KingdomDiplomacyVM __instance,
        KingdomTruceItemVM item,
        float actionInfluenceCost,
        ref TextObject disabledReason,
        ref bool __result)
        {
            if (item == null || Clan.PlayerClan?.Kingdom == null)
                return true;

            Kingdom playerKingdom = Clan.PlayerClan.Kingdom;
            Kingdom targetKingdom = item.Faction2 as Kingdom;

            if (targetKingdom == null)
                return true;

            if (!playerKingdom._unresolvedDecisions.OfType<StartAllianceDecision>().Any(d => d.Kingdom == playerKingdom && d.KingdomToStartAllianceWith == targetKingdom)
                && !AllianceOfferPendingRegistry.IsPending(playerKingdom.StringId, targetKingdom.StringId))
            {
                return true;
            }
            __result = false;
            disabledReason = new TextObject("You have already offered an alliance to this kingdom.");
            return false;
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

    public static class PeaceOfferPendingRegistry
    {
        private static readonly object Lock = new();
        internal static readonly HashSet<(string RequestingKingdomId, string TargetKingdomId)> _pending = new();

        public static void Set(string requestingKingdomId, string targetKingdomId, bool isPending)
        {
            var key = (requestingKingdomId, targetKingdomId);

            lock (Lock)
            {
                if (isPending)
                    _pending.Add(key);
                else
                    _pending.Remove(key);
            }
        }

        public static bool IsPending(string requestingKingdomId, string targetKingdomId)
        {
            lock (Lock)
            {
                return _pending.Contains((requestingKingdomId, targetKingdomId));
            }
        }

        public static (string RequestingKingdomId, string TargetKingdomId)[] Snapshot()
        {
            lock (Lock)
            {
                return _pending.ToArray();
            }
        }

        public static void RestoreAll(
            (string RequestingKingdomId, string TargetKingdomId)[] entries)
        {
            lock (Lock)
            {
                _pending.Clear();

                foreach (var entry in entries)
                    _pending.Add(entry);
            }
        }
    }

    public static class AllianceOfferPendingRegistry
    {
        private static readonly object Lock = new();
        internal static readonly HashSet<(string RequestingKingdomId, string TargetKingdomId)> _pending = new();

        public static void Set(string requestingKingdomId, string targetKingdomId, bool isPending)
        {
            var key = (requestingKingdomId, targetKingdomId);

            lock (Lock)
            {
                if (isPending)
                    _pending.Add(key);
                else
                    _pending.Remove(key);
            }
        }

        public static bool IsPending(string requestingKingdomId, string targetKingdomId)
        {
            lock (Lock)
            {
                return _pending.Contains((requestingKingdomId, targetKingdomId));
            }
        }

        public static (string RequestingKingdomId, string TargetKingdomId)[] Snapshot()
        {
            lock (Lock)
            {
                return _pending.ToArray();
            }
        }

        public static void RestoreAll(
            (string RequestingKingdomId, string TargetKingdomId)[] entries)
        {
            lock (Lock)
            {
                _pending.Clear();

                foreach (var entry in entries)
                    _pending.Add(entry);
            }
        }
    }

    internal interface IClientClanStrengthRefresher
    {
        void Refresh(IFaction faction);
    }

    internal class ClientClanStrengthRefresher : IClientClanStrengthRefresher
    {
        public void Refresh(IFaction faction)
        {
            if (faction is Kingdom kingdom)
            {
                foreach (var clan in kingdom.Clans)
                {
                    clan.UpdateCurrentStrength();
                }
            }
            else if (faction is Clan clan)
            {
                clan.UpdateCurrentStrength();
            }
        }
    }

    [HarmonyPatch(typeof(KingdomWarItemVM), nameof(KingdomWarItemVM.UpdateDiplomacyProperties))]
    internal class KingdomWarItemVMPatches
    {
        [HarmonyPrefix]
        private static void Prefix(KingdomWarItemVM __instance)
        {
            if (!ContainerProvider.TryResolve<IClientClanStrengthRefresher>(out var refresher)) return;

            refresher.Refresh(__instance.Faction1);
            refresher.Refresh(__instance.Faction2);
        }
    }

    [HarmonyPatch(typeof(KingdomTruceItemVM), nameof(KingdomTruceItemVM.UpdateDiplomacyProperties))]
    internal class KingdomTruceItemVMPatches
    {
        [HarmonyPrefix]
        private static void Prefix(KingdomTruceItemVM __instance)
        {
            if (!ContainerProvider.TryResolve<IClientClanStrengthRefresher>(out var refresher)) return;

            refresher.Refresh(__instance.Faction1);
            refresher.Refresh(__instance.Faction2);
        }
    }
    [HarmonyPatch(typeof(DefaultAllianceModel), nameof(DefaultAllianceModel.GetCallToWarCost))]
    internal class GetCallToWarCostPatches
    {
        [HarmonyPrefix]
        private static void Prefix(DefaultAllianceModel __instance, Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
        {
            if (!ContainerProvider.TryResolve<IClientClanStrengthRefresher>(out var refresher)) return;

            refresher.Refresh(callingKingdom);
            refresher.Refresh(calledKingdom);
            refresher.Refresh(kingdomToCallToWarAgainst);
        }
    }
}
