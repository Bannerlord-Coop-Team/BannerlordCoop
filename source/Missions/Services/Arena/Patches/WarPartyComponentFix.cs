using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Missions.Services.Arena.Patches
{
    [HarmonyPatch(typeof(WarPartyComponent))]
    internal class WarPartyComponentFix
    {
        [HarmonyPatch(nameof(WarPartyComponent.Clan), MethodType.Getter)]
        private static bool Prefix(ref WarPartyComponent __instance, ref Clan __result)
        {
            if (__instance.MobileParty?.ActualClan == null)
            {
                __result = Clan.PlayerClan;
                return false;
            }

            return true;
        }
    }
}
