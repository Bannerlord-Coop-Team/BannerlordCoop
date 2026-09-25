using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Armies;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class CoopClanKingdomPermissionsPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.GetCanChangeKingdomNameWithReason));
        yield return AccessTools.Method(typeof(KingdomClanVM), nameof(KingdomClanVM.GetCanSupportCurrentClanWithReason));
        yield return AccessTools.Method(typeof(KingdomClanVM), nameof(KingdomClanVM.GetCanExpelCurrentClanWithReason));
        yield return AccessTools.Method(typeof(KingdomPoliciesVM), nameof(KingdomPoliciesVM.GetCanProposeOrDisavowPolicyWithReason));
        yield return AccessTools.Method(typeof(KingdomArmyVM), nameof(KingdomArmyVM.GetCanManageCurrentArmyWithReason));
        yield return AccessTools.Method(typeof(KingdomArmyVM), nameof(KingdomArmyVM.GetCanDisbandCurrentArmyWithReason));
        yield return AccessTools.Method(typeof(KingdomDiplomacyVM), nameof(KingdomDiplomacyVM.GetAreProposalActionsEnabledWithReason));
        yield return AccessTools.Method(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanPlayerCreateArmy));
        yield return AccessTools.Method(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetCanManageCurrentArmyWithReason));
    }

    [HarmonyPrefix]
    public static bool CanManageKingdomPrefix(ref bool __result, ref TextObject disabledReason)
    {
        if (ModInformation.IsServer) return true;
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return true;

        __result = false;
        disabledReason = GameTexts.FindText("str_coop_clan_kingdom_leader_only");
        return false;
    }
}

[HarmonyPatch]
internal class CoopClanKingdomActionsPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.ExecuteKingdomAction));
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.ExecuteChangeKingdomName));
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.OnConfirmAbdicateLeadership));
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.OnConfirmLeaveKingdom));
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.OnConfirmLeaveKingdomWithOption));
        yield return AccessTools.Method(typeof(KingdomManagementVM), nameof(KingdomManagementVM.OnChangeKingdomNameDone));
        yield return AccessTools.Method(typeof(KingdomClanVM), nameof(KingdomClanVM.ExecuteSupport));
        yield return AccessTools.Method(typeof(KingdomClanVM), nameof(KingdomClanVM.ExecuteExpelCurrentClan));
        yield return AccessTools.Method(typeof(KingdomPoliciesVM), nameof(KingdomPoliciesVM.ExecuteProposeOrDisavow));
        yield return AccessTools.Method(typeof(KingdomSettlementVM), nameof(KingdomSettlementVM.ExecuteAnnex));
        yield return AccessTools.Method(typeof(KingdomGiftFiefPopupVM), nameof(KingdomGiftFiefPopupVM.ExecuteGiftSettlement));
        yield return AccessTools.Method(typeof(KingdomArmyVM), nameof(KingdomArmyVM.ExecuteManageArmy));
        yield return AccessTools.Method(typeof(KingdomArmyVM), nameof(KingdomArmyVM.ExecuteDisbandCurrentArmy));
        yield return AccessTools.Method(typeof(ArmyManagementVM), nameof(ArmyManagementVM.ExecuteDone));
        yield return AccessTools.Method(typeof(KingdomDiplomacyProposalActionItemVM), nameof(KingdomDiplomacyProposalActionItemVM.ExecuteAction));
        yield return AccessTools.Method(typeof(DecisionOptionVM), nameof(DecisionOptionVM.ExecuteSelection));
        yield return AccessTools.Method(typeof(DecisionOptionVM), nameof(DecisionOptionVM.OnSupportStrengthChange));
        yield return AccessTools.Method(typeof(DecisionItemBaseVM), nameof(DecisionItemBaseVM.ExecuteFinalSelection));
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool ManageKingdomPrefix()
        => CoopClanPermissions.CanManageClan(Hero.MainHero.Clan);
}

[HarmonyPatch]
internal class CoopClanKingdomControlsPatches
{
    [HarmonyPatch(typeof(KingdomManagementVM), nameof(KingdomManagementVM.GetIsKingdomActionEnabledWithReason))]
    [HarmonyPostfix]
    public static void KingdomActionPostfix(ref bool __result, List<TextObject> disabledReasons)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return;

        __result = false;
        disabledReasons.Clear();
        disabledReasons.Add(GameTexts.FindText("str_coop_clan_kingdom_leader_only"));
    }

    [HarmonyPatch(typeof(KingdomSettlementVM), nameof(KingdomSettlementVM.SetCurrentSelectedSettlement))]
    [HarmonyPostfix]
    public static void SelectSettlementPostfix(KingdomSettlementVM __instance)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return;

        __instance.CanAnnexCurrentSettlement = false;
        __instance.AnnexHint.HintText = GameTexts.FindText("str_coop_clan_kingdom_leader_only");
    }

    [HarmonyPatch(typeof(DecisionOptionVM), nameof(DecisionOptionVM.RefreshCanChooseOption))]
    [HarmonyPostfix]
    public static void RefreshDecisionOptionPostfix(DecisionOptionVM __instance)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return;

        __instance.CanBeChosen = false;
        __instance.IsSupportOption1Enabled = false;
        __instance.IsSupportOption2Enabled = false;
        __instance.IsSupportOption3Enabled = false;
        __instance.OptionHint.HintText = GameTexts.FindText("str_coop_clan_kingdom_leader_only");
    }

    [HarmonyPatch(typeof(DecisionItemBaseVM), nameof(DecisionItemBaseVM.RefreshCanEndDecision))]
    [HarmonyPostfix]
    public static void RefreshDecisionPostfix(DecisionItemBaseVM __instance)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return;

        __instance.CanEndDecision = false;
        __instance.EndDecisionHint.HintText = GameTexts.FindText("str_coop_clan_kingdom_leader_only");
    }
}
