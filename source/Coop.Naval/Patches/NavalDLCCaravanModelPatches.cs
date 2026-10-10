using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem;

namespace Coop.Naval.Patches;

/// <summary>
/// Vanilla uses a Hero.MainHero check for the caravan's owner to give it extra money when forming.
/// Without this patch, player naval caravans are treated as AI naval caravans in terms of their starting balance.
/// This takes a lot longer to build up to being profitable as they have less money to build up a roster of trade goods.
/// </summary>
[HarmonyPatch(typeof(NavalDLCCaravanModel))]
internal class NavalDLCCaravanModelPatches
{
    [HarmonyPatch(nameof(NavalDLCCaravanModel.GetInitialTradeGold))]
    [HarmonyPrefix]
    public static bool GetInitialTradeGoldPrefix(NavalDLCCaravanModel __instance, ref int __result, Hero owner, bool navalCaravan, bool largeCaravan)
    {
        if (navalCaravan)
        {
            int fromType = 30000;
            int fromPlayerOwner = (owner.IsPlayerHero()) ? 5000 : 0;
            if (largeCaravan)
            {
                fromType = 40000;
            }
            __result = fromType + fromPlayerOwner;
            return false;
        }
        __result = __instance.BaseModel.GetInitialTradeGold(owner, navalCaravan, largeCaravan);

        return false;
    }
}
