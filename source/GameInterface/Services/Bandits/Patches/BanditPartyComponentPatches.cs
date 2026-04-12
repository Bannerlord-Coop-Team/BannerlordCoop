using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Bandits.Patches
{
    [HarmonyPatch(typeof(BanditPartyComponent))]
    internal class BanditPartyComponentPatches
    {
        [HarmonyPatch(nameof(BanditPartyComponent.PartyOwner))]
        [HarmonyPatch(MethodType.Getter)]
        [HarmonyPrefix]
        static bool PartyOwnerGetter(BanditPartyComponent __instance)
        {
            if (__instance.MobileParty == null) return false;

            return true;
        }
    }
}
