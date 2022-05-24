using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace Coop.Mod.GameSync.Fief
{
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.Fief), "FoodStocks", MethodType.Setter)]
    class FiefPatch
    {
        static void Postfix(TaleWorlds.CampaignSystem.Fief __instance)
        {
            if (!Coop.IsServer)
            {
                return;
            }

            FiefSync.BroadcastChangeFoodStock(__instance);
        }
    }
}
