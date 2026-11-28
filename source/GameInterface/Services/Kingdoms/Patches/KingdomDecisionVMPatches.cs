using Common.Logging;
using Common;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Kingdoms.Extentions;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>Opens the kingdom screen when a fief result arrives while the player is on the map.</summary>
    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.OnFrameTick))]
    internal class SettlementDecisionResultMapPatch
    {
        private static void Postfix(MapScreen __instance)
        {
            if (ModInformation.IsServer || ScreenManager.TopScreen != __instance) return;
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager) ||
                !voteManager.TryGetPendingSettlementDecisionResult(out _)) return;
            if (!CanOpenKingdomScreen(__instance)) return;

            __instance.OpenKingdom();
        }

        // Match the native map conditions for opening the kingdom decision screen.
        private static bool CanOpenKingdomScreen(MapScreen mapScreen)
        {
            if (mapScreen.IsInMenu || mapScreen.IsInBattleSimulation || mapScreen.IsInArmyManagement) return false;
            if (mapScreen.IsMarriageOfferPopupActive || mapScreen.IsHeirSelectionPopupActive ||
                InformationManager.IsAnyInquiryActive()) return false;
            if (mapScreen.IsMapCheatsActive || mapScreen.IsMapIncidentActive || mapScreen.IsOverlayContextMenuEnabled ||
                mapScreen.EncyclopediaScreenManager.IsEncyclopediaOpen) return false;

            return CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out _);
        }
    }

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

        // Skip submitted ballots and show queued results before prompting for a new vote.
        [HarmonyPatch(nameof(KingdomDecisionsVM.OnFrameTick))]
        [HarmonyPrefix]
        private static void OnFrameTickPrefix(KingdomDecisionsVM __instance)
        {
            MarkSubmittedDecisionsExamined(__instance);
            if (__instance.CurrentDecision != null || !__instance._shouldCheckForDecision) return;
            if (!TryGetVoteManager(out var voteManager) || !voteManager.TryGetPendingSettlementDecisionResult(out var decision)) return;
            if (InformationManager.IsAnyInquiryActive() || !CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out _)) return;

            DecisionItemBaseVM item = __instance.GetDecisionItemBasedOnType(decision);
            if (!voteManager.TryShowPendingSettlementDecisionResult(item))
            {
                // Vanilla ExecuteDone clears this listener, and OnFinalize leaves it registered.
                CampaignEvents.KingdomDecisionConcluded.ClearListeners(item);
                item.OnFinalize();
                return;
            }

            item.SetDoneInputKey(__instance.DoneInputKey);
            __instance._shouldCheckForDecision = false;
            __instance.CurrentDecision = item;
        }

        // Vanilla prefers the last solved decision's follow-up without checking the examined list.
        [HarmonyPatch(nameof(KingdomDecisionsVM.OnFrameTick))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OnFrameTickTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getFollowUpDecision = AccessTools.Method(typeof(KingdomDecision), nameof(KingdomDecision.GetFollowUpDecision));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(getFollowUpDecision))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(KingdomDecisionsVMPatches), nameof(GetUnsubmittedFollowUpDecision));
                }

                yield return instruction;
            }
        }

        // A submitted follow-up gives way to the next unvoted decision.
        private static KingdomDecision GetUnsubmittedFollowUpDecision(KingdomDecision solvedDecision)
        {
            KingdomDecision followUpDecision = solvedDecision.GetFollowUpDecision();
            if (!TryGetVoteManager(out var voteManager)) return followUpDecision;

            return voteManager.IsSubmittedDecision(followUpDecision) ? null : followUpDecision;
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleNextDecision))]
        [HarmonyPrefix]
        private static void HandleNextDecisionPrefix(KingdomDecisionsVM __instance)
        {
            MarkSubmittedDecisionsExamined(__instance);
        }

        private static void MarkSubmittedDecisionsExamined(KingdomDecisionsVM decisionsVm)
        {
            if (!TryGetVoteManager(out var voteManager)) return;
            IEnumerable<KingdomDecision> decisions = Clan.PlayerClan?.Kingdom?.UnresolvedDecisions;
            if (decisions == null) return;

            foreach (KingdomDecision decision in decisions)
            {
                if (decisionsVm._examinedDecisionsSinceInit.Contains(decision)) continue;
                if (!voteManager.IsSubmittedDecision(decision)) continue;

                decisionsVm._examinedDecisionsSinceInit.Add(decision);
            }
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.OnFrameTick))]
        [HarmonyPostfix]
        private static void OnFrameTickPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;
            DecisionItemBaseVM currentDecision = __instance.CurrentDecision;
            if (currentDecision == null) return;
            if (voteManager.IsShowingSettlementDecisionResult(currentDecision))
            {
                // The result screen has no waiting status, so hide the overlay.
                KingdomDecisionWaitingStatusWidgetPatch.Refresh(__instance, header: null, columns: null);
                return;
            }

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

        // Drops a suppressed decision and resumes the checks, otherwise bypasses vanilla's single-clan shortcut for a pending peace or alliance offer.
        [HarmonyPatch(typeof(KingdomDecisionsVM), nameof(KingdomDecisionsVM.RefreshWith))]
        [HarmonyPrefix]
        public static bool RefreshWithPrefix(KingdomDecisionsVM __instance, KingdomDecision decision)
        {
            if (TryGetVoteManager(out var voteManager) && voteManager.ShouldSuppressLocalDecision(decision))
            {
                // Vanilla's resolve prompt stopped the checks, so resume them like its cancel button does.
                __instance._shouldCheckForDecision = true;
                return false;
            }

            if ((CoopKingdomElection.IsPendingPlayerPeaceOffer(decision) || CoopKingdomElection.IsPendingPlayerAllianceOffer(decision)) && decision.IsSingleClanDecision())
            {
                __instance._shouldCheckForDecision = false;
                DecisionItemBaseVM decisionItem = __instance.GetDecisionItemBasedOnType(decision);

                __instance.CurrentDecision = decisionItem;
                __instance.CurrentDecision.SetDoneInputKey(__instance.DoneInputKey);
                return false;
            }
            return true;
        }
        // Singleclan kingdoms resolve decisions instantly (IsSingleClanDecision),
        // so the vanilla "will resolve in 48h" popup is always false for them.
        // Skip straight to RefreshWith instead of showing it.
        [HarmonyPatch(nameof(KingdomDecisionsVM.HandleDecision))]
        [HarmonyPriority(Priority.High)]
        [HarmonyPrefix]
        private static bool HandleDecisionSingleClanSkipPrefix(KingdomDecisionsVM __instance, KingdomDecision curDecision)
        {
            if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out _) || curDecision == null || !curDecision.IsPlayerParticipant || !curDecision.IsSingleClanDecision()) return true;
            if (curDecision.ShouldBeCancelled()) return true;

            if (!TryGetVoteManager(out var voteManager)) return true;
            if (voteManager.ShouldSuppressLocalDecision(curDecision)) return true;
            __instance._examinedDecisionsSinceInit.Add(curDecision);
            __instance._shouldCheckForDecision = false;
            __instance.RefreshWith(curDecision);

            return false;
        }
    }

    [HarmonyPatch(typeof(DecisionItemBaseVM))]
    internal class DecisionItemBaseVMPatches
    {
        private static readonly TextObject SubmittedCloseHint =
            new TextObject("Your vote is submitted. Close this screen; the result is announced when voting ends.");

        [HarmonyPatch("OnChangeVote")]
        [HarmonyPostfix]
        private static void OnChangeVotePostfix(DecisionOptionVM __0)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;

            voteManager.TryPublishVote(__0);
        }

        [HarmonyPatch(nameof(DecisionItemBaseVM.ExecuteFinalSelection))]
        [HarmonyPrefix]
        private static bool ExecuteFinalSelectionPrefix(DecisionItemBaseVM __instance)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return true;
            if (voteManager.IsShowingSettlementDecisionResult(__instance)) return false;
            if (voteManager.IsSubmittedDecision(__instance.KingdomDecisionMaker?._decision))
            {
                voteManager.DismissSubmittedDecisionItem(__instance);
                return false;
            }
            if (!voteManager.ShouldBlockLocalResolution(__instance)) return true;

            voteManager.TryPublishFinalVote(__instance);
            return false;
        }

        // Done closes a reopened submitted decision; the ballot itself stays locked.
        [HarmonyPatch("RefreshCanEndDecision")]
        [HarmonyPostfix]
        private static void RefreshCanEndDecisionPostfix(DecisionItemBaseVM __instance)
        {
            if (!__instance._finalSelectionDone || !__instance.IsActive) return;
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;
            if (voteManager.IsShowingSettlementDecisionResult(__instance)) return;
            if (!voteManager.IsSubmittedDecision(__instance.KingdomDecisionMaker?._decision)) return;

            __instance.CanEndDecision = true;
            if (__instance.EndDecisionHint != null)
            {
                __instance.EndDecisionHint.HintText = SubmittedCloseHint;
            }
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

    [HarmonyPatch(typeof(KingdomDiplomacyVM))]
    public class KingdomDiplomacyVMPatches
    {
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
