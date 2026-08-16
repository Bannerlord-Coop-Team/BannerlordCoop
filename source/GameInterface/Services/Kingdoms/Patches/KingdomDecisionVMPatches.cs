using Common;
using HarmonyLib;
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

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(KingdomDecisionsVM))]
    internal class KingdomDecisionsVMPatches
    {
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
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.RefreshWith))]
        [HarmonyPostfix]
        private static void RefreshWithPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;

            voteManager.RegisterDecisionItem(__instance.CurrentDecision);
        }

        [HarmonyPatch(nameof(KingdomDecisionsVM.OnFrameTick))]
        [HarmonyPostfix]
        private static void OnFrameTickPostfix(KingdomDecisionsVM __instance)
        {
            if (!TryGetVoteManager(out var voteManager)) return;

            voteManager.RefreshDecisionWaitingStatus(__instance.CurrentDecision);
        }

        internal static bool TryGetVoteManager(out IKingdomDecisionVoteManager voteManager)
        {
            return ContainerProvider.TryResolve(out voteManager);
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
        private static bool ExecuteFinalSelectionPrefix(DecisionItemBaseVM __instance)
        {
            if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return true;
            if (!voteManager.ShouldBlockLocalResolution(__instance)) return true;

            voteManager.TryPublishFinalVote(__instance);
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
                if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return;
                if (!voteManager.ShouldDisableResolveDecision(resolveDecision)) continue;

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
    internal interface IClientClanStrengthRefresher
    {
        void Refresh(IFaction faction);
    }

    internal class ClientClanStrengthRefresher : IClientClanStrengthRefresher
    {
        public void Refresh(IFaction faction)
        {
            if (ModInformation.IsServer) return;

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
}