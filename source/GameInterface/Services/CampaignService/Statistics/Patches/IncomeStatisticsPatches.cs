using Common;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.CampaignService.Statistics.Patches;

/// <summary>
/// Vanilla only fires OnPlayerEarnedGoldFromAsset for Clan.PlayerClan, which is null on the server,
/// so the asset income statistics are recorded here for the clan leader instead
/// </summary>
[HarmonyPatch(typeof(DefaultClanFinanceModel))]
[HarmonyPatchCategory(GameInterface.HARMONY_GAME_STARTED_CATEGORY)]
internal class IncomeStatisticsPatches
{
    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddIncomeFromParty))]
    [HarmonyPostfix]
    public static void AddIncomeFromPartyPostfix(MobileParty party, Clan clan, bool applyWithdrawals, int __result)
    {
        if (party.IsCaravan) RecordAssetIncome(clan.Leader, DefaultClanFinanceModel.AssetIncomeType.Caravan, applyWithdrawals, __result);
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateTownIncomeFromTariffs))]
    [HarmonyPostfix]
    public static void CalculateTownIncomeFromTariffsPostfix(Clan clan, bool applyWithdrawals, ExplainedNumber __result)
    {
        RecordAssetIncome(clan.Leader, DefaultClanFinanceModel.AssetIncomeType.Taxes, applyWithdrawals, (int)__result.ResultNumber);
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateVillageIncome))]
    [HarmonyPostfix]
    public static void CalculateVillageIncomePostfix(Clan clan, bool applyWithdrawals, int __result)
    {
        RecordAssetIncome(clan.Leader, DefaultClanFinanceModel.AssetIncomeType.Taxes, applyWithdrawals, __result);
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateHeroIncomeFromWorkshops))]
    [HarmonyPrefix]
    public static void CalculateHeroIncomeFromWorkshopsPrefix(ref ExplainedNumber goldChange, out float __state) => __state = goldChange.ResultNumber;

    [HarmonyPatch(nameof(DefaultClanFinanceModel.CalculateHeroIncomeFromWorkshops))]
    [HarmonyPostfix]
    public static void CalculateHeroIncomeFromWorkshopsPostfix(Hero hero, ref ExplainedNumber goldChange, bool applyWithdrawals, float __state)
    {
        RecordAssetIncome(hero, DefaultClanFinanceModel.AssetIncomeType.Workshop, applyWithdrawals, (int)(goldChange.ResultNumber - __state));
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddIncomeFromTribute))]
    [HarmonyPrefix]
    public static void AddIncomeFromTributePrefix(ref ExplainedNumber goldChange, out float __state) => __state = goldChange.ResultNumber;

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddIncomeFromTribute))]
    [HarmonyPostfix]
    public static void AddIncomeFromTributePostfix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, float __state)
    {
        RecordAssetIncome(clan.Leader, DefaultClanFinanceModel.AssetIncomeType.TributesEarned, applyWithdrawals, (int)(goldChange.ResultNumber - __state));
    }

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddIncomeFromCallToWarAgrements))]
    [HarmonyPrefix]
    public static void AddIncomeFromCallToWarAgrementsPrefix(ref ExplainedNumber goldChange, out float __state) => __state = goldChange.ResultNumber;

    [HarmonyPatch(nameof(DefaultClanFinanceModel.AddIncomeFromCallToWarAgrements))]
    [HarmonyPostfix]
    public static void AddIncomeFromCallToWarAgrementsPostfix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, float __state)
    {
        RecordAssetIncome(clan.Leader, DefaultClanFinanceModel.AssetIncomeType.TributesEarned, applyWithdrawals, (int)(goldChange.ResultNumber - __state));
    }

    private static void RecordAssetIncome(Hero earner, DefaultClanFinanceModel.AssetIncomeType assetType, bool applyWithdrawals, int amount)
    {
        if (!applyWithdrawals || ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IStatisticsCampaignBehaviorInterface>(out var statistics)) return;

        statistics.OnPlayerEarnedGoldFromAsset(earner, assetType, amount);
    }
}
