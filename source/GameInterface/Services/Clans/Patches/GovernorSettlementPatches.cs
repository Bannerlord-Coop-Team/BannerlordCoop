using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Governor effects apply only while the governor is in town, so a player governor counts as present in their town.
/// </summary>
[HarmonyPatch]
internal class GovernorSettlementPatches
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PerkHelper), nameof(PerkHelper.AddPerkBonusForTown));
        yield return AccessTools.Method(typeof(PerkHelper), nameof(PerkHelper.GetPerkValueForTown));
        yield return AccessTools.Method(typeof(DefaultBuildingConstructionModel), nameof(DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal));
        yield return AccessTools.Method(typeof(DefaultBuildingConstructionModel), nameof(DefaultBuildingConstructionModel.GetBoostCost));
        yield return AccessTools.Method(typeof(DefaultSettlementFoodModel), nameof(DefaultSettlementFoodModel.CalculateTownFoodChangeInternal));
        yield return AccessTools.Method(typeof(DefaultSettlementLoyaltyModel), nameof(DefaultSettlementLoyaltyModel.CalculateLoyaltyChangeInternal));
        yield return AccessTools.Method(typeof(DefaultWorkshopModel), nameof(DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction));
        yield return AccessTools.Method(typeof(DefaultSettlementProsperityModel), nameof(DefaultSettlementProsperityModel.CalculateHearthChangeInternal));
        yield return AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.DailyTickSettlement));
    }

    // Swaps only the hero's CurrentSettlement; the static Settlement.CurrentSettlement that DailyTickSettlement also reads is left alone.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var currentSettlement = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        var governorSettlement = AccessTools.Method(typeof(HeroExtensions), nameof(HeroExtensions.GetGovernorSettlement));
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
}
