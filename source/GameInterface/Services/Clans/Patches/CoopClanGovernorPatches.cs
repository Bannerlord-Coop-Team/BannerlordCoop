using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GameInterface.Services.Heroes.Extensions;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class CoopClanGovernorPatches
{
    [HarmonyPatch(typeof(ClanFiefsVM), nameof(ClanFiefsVM.GetCanChangeGovernor))]
    [HarmonyPrefix]
    public static bool GetCanChangeGovernorPrefix(ref bool __result, ref TextObject disabledReason)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return true;

        __result = false;
        disabledReason = GameTexts.FindText("str_coop_clan_governor_leader_only");
        return false;
    }

    [HarmonyPatch(typeof(TownManagementVM), nameof(TownManagementVM.GetCanChangeGovernor))]
    [HarmonyPrefix]
    public static bool TownGetCanChangeGovernorPrefix(ref bool __result, ref TextObject disabledReason)
        => GetCanChangeGovernorPrefix(ref __result, ref disabledReason);

    [HarmonyPatch(typeof(ClanFiefsVM), nameof(ClanFiefsVM.GetGovernorCandidates))]
    [HarmonyPostfix]
    public static void GetGovernorCandidatesPostfix(ref IEnumerable<ClanCardSelectionItemInfo> __result)
    {
        __result = __result.Where(candidate => !(candidate.Identifier is Hero hero) ||
            (hero.IsPlayerHero()
                ? hero.Clan == Hero.MainHero.Clan && hero != hero.Clan.Leader
                : CoopClanPermissions.CanManageHero(hero))).ToList();
    }

    [HarmonyPatch(typeof(FactionHelper), nameof(FactionHelper.IsMainClanMemberAvailableForSendingSettlementAsGovernor))]
    [HarmonyPrefix]
    public static bool IsAvailableAsGovernorPrefix(Hero hero, Settlement settlementOfGovernor,
        ref bool __result, ref TextObject explanation)
    {
        if (!hero.IsPlayerHero()) return true;

        __result = CoopClanPermissions.CanManageClan(hero.Clan) && hero.GovernorOf == null &&
            settlementOfGovernor == null && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
        explanation = __result ? null : GameTexts.FindText("str_coop_clan_player_governor_unavailable");
        return false;
    }

    [HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetTeleportationDelayText))]
    [HarmonyPrefix]
    public static bool GetTeleportationDelayTextPrefix(Hero hero, PartyBase target, ref TextObject __result)
    {
        if (hero == null || !hero.IsPlayerHero() || target?.Settlement?.OwnerClan != hero.Clan) return true;

        __result = GameTexts.FindText("str_coop_clan_player_governor_effects");
        return false;
    }

    [HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetGovernorSelectionConfirmationPopupTexts))]
    [HarmonyPrefix]
    public static bool GetGovernorConfirmationTextsPrefix(Hero newGovernor, Settlement settlement,
        ref (TextObject titleText, TextObject bodyText) __result)
    {
        if (newGovernor == null || !newGovernor.IsPlayerHero() || settlement == null) return true;

        var body = GameTexts.FindText("str_coop_clan_assign_player_governor");
        body.SetTextVariable("HERO", newGovernor.Name);
        body.SetTextVariable("SETTLEMENT", settlement.Name);
        __result = (GameTexts.FindText("str_clan_assign_governor"), body);
        return false;
    }

    [HarmonyPatch(typeof(PerkHelper), nameof(PerkHelper.AddPerkBonusForTown))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorBonusTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var currentSettlement = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        var governorSettlement = AccessTools.Method(typeof(CoopClanGovernorPatches), nameof(GetGovernorSettlement));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(currentSettlement))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = governorSettlement;
            }
            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(PerkHelper), nameof(PerkHelper.GetPerkValueForTown))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorPerkValueTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    [HarmonyPatch(typeof(DefaultBuildingConstructionModel), nameof(DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorConstructionTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    // v1.5 personality trait effects that apply only while the governor is in town. Each of these
    // methods reads CurrentSettlement once, on the governor.
    [HarmonyPatch(typeof(DefaultBuildingConstructionModel), nameof(DefaultBuildingConstructionModel.GetBoostCost))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorBoostCostTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    [HarmonyPatch(typeof(DefaultSettlementFoodModel), nameof(DefaultSettlementFoodModel.CalculateTownFoodChangeInternal))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorFoodTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    [HarmonyPatch(typeof(DefaultSettlementLoyaltyModel), nameof(DefaultSettlementLoyaltyModel.CalculateLoyaltyChangeInternal))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorLoyaltyTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    [HarmonyPatch(typeof(DefaultWorkshopModel), nameof(DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorWorkshopTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    [HarmonyPatch(typeof(DefaultSettlementProsperityModel), nameof(DefaultSettlementProsperityModel.CalculateHearthChangeInternal))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorHearthTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    // The other CurrentSettlement read here is the static Settlement.CurrentSettlement, which the swap leaves alone.
    [HarmonyPatch(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.DailyTickSettlement))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GovernorRebellionTranspiler(IEnumerable<CodeInstruction> instructions)
        => GovernorBonusTranspiler(instructions);

    /// <summary>A player governor counts as present in the town they govern, wherever they are.</summary>
    internal static Settlement GetGovernorSettlement(Hero hero)
        => hero.IsPlayerHero() && hero.GovernorOf != null ? hero.GovernorOf.Settlement : hero.CurrentSettlement;
}
