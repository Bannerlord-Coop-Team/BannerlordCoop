using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(DefaultSettlementAccessModel))]
internal class DefaultSettlementAccessModelPatches
{
    [HarmonyPatch(nameof(DefaultSettlementAccessModel.CanMainHeroGoToArena))]
    [HarmonyPrefix]
    public static bool CanMainHeroGoToArenaPrefix(ref bool __result, ref bool disableOption, ref TextObject disabledText)
    {
        if (Campaign.Current.IsMainHeroDisguised)
            return true;

        __result = true;
        disableOption = false;
        disabledText = null;
        return false;
    }

    [HarmonyPatch(nameof(DefaultSettlementAccessModel.CanMainHeroManageTown))]
    [HarmonyPostfix]
    public static void CanMainHeroManageTownPostfix(ref bool __result, Settlement settlement)
    {
        if (settlement.IsTown && settlement.OwnerClan == Hero.MainHero.Clan)
        {
            __result = true;
        }
    }
}
