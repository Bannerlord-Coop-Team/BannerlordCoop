using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(BanditsCampaignBehavior), "InitBanditParty")]
    internal class BanditsCampaignBehaviorPatch
    {
        public static EventHandler<MobileParty> OnBanditAdded;

        private static bool Prefix(BanditsCampaignBehavior __instance, ref MobileParty banditParty, ref Clan faction,
            ref Settlement homeSettlement)
        {
            OnBanditAdded?.Invoke(__instance, banditParty);
            return true;
        }
    }
}