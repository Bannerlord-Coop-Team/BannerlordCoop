using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch.Party
{
    [HarmonyPatch(typeof(LordPartyComponent), "InitializeLordPartyProperties")]
    internal class LordPartyComponentPatch
    {
        public static EventHandler<MobileParty> OnLordPartySpawned;

        private static bool Suffix(LordPartyComponent __instance, ref MobileParty mobileParty, ref Vec2 position,
            ref float spawnRadius, ref Settlement spawnSettlement)
        {
            OnLordPartySpawned?.Invoke(__instance, mobileParty);
            return true;
        }
    }
}