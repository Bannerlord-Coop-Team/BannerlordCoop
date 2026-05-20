using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Localization;

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

        [HarmonyPatch(nameof(BanditPartyComponent.Name))]
        [HarmonyPatch(MethodType.Getter)]
        [HarmonyPrefix]
        static bool NameGetter(BanditPartyComponent __instance, ref TextObject __result)
        {
            if (__instance.MobileParty?.MapFaction == null)
            {
                TextObject textObject = new TextObject("NameFailed - BanditPartyPatch");
                textObject.SetTextVariable("IS_BANDIT", 1);
                __result = textObject;
                return false;
            }

            return true;
        }
    }
}
