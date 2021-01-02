using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Localization;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(BanditsCampaignBehavior), "InitBanditParty")]
    class BanditsCampaignBehaviorPatch
    {
        public static EventHandler<MobileParty> OnBanditAdded;

        static bool Prefix(BanditsCampaignBehavior __instance, ref MobileParty banditParty, ref TextObject name, ref Clan faction, ref Settlement homeSettlement)
        {
            OnBanditAdded?.Invoke(__instance, banditParty);
            return true;
        }
    }
}
