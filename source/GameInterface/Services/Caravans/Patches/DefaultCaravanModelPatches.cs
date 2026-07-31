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
    [HarmonyPostfix]
    public static void GetInitialTradeGoldPrefix(ref int __result, Hero owner)
    {
        // Add the same 5000 extra for any player hero as vanilla does for Hero.MainHero
        if (owner != null && owner.IsPlayerHero())
        {
            __result += 5000;
        }
    }
}
