using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Fiefs.Messages;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs.Patches
{
    /// <summary>
    /// Modify functionnality for Fief
    /// </summary>
    [HarmonyPatch(typeof(Fief))]
    public class FiefPatches
    {

        [HarmonyPatch(nameof(Fief.FoodStocks), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool FiefFoodStocksPrefix(ref Fief __instance, ref float value)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;
            if (__instance.FoodStocks == value) return false;

            var message = new FiefFoodStockChanged(__instance.StringId, value);
            MessageBroker.Instance.Publish(__instance, message);
            return true;
        }

        public static void ChangeFiefFoodStock(Fief fief, float foodStocks)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    fief.FoodStocks = foodStocks;
                }
            });

        }
    }
}
