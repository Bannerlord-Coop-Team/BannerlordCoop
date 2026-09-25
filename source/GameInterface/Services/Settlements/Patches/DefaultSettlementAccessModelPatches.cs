using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(DefaultSettlementAccessModel))]
internal class DefaultSettlementAccessModelPatches
{
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
