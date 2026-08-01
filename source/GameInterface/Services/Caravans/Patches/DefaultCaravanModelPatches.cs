using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Caravans.Patches;

/// <summary>
/// Vanilla uses a Hero.MainHero check for the caravan's owner to give it extra money when forming.
/// Without this patch, player caravans are treated as AI caravans in terms of their starting balance.
/// This takes a lot longer to build up to being profitable as they have less money to build up a roster of trade goods.
/// </summary>
[HarmonyPatch(typeof(DefaultCaravanModel))]
internal class DefaultCaravanModelPatches
{
    [HarmonyPatch(nameof(DefaultCaravanModel.GetInitialTradeGold))]
    [HarmonyPrefix]
    public static bool GetInitialTradeGoldPrefix(ref int __result, Hero owner, bool navalCaravan, bool largeCaravan)
    {
        int fromType = 10000;
        int fromPlayerOwner = (owner.IsPlayerHero()) ? 5000 : 0;
        if (largeCaravan)
        {
            fromType = 17500;
        }
        __result = fromType + fromPlayerOwner;

        return false;
    }
}
