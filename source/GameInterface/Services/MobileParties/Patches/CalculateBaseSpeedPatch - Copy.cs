using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class CalculateBaseSpeedPatch2
{
    [HarmonyPatch("TaleWorlds.CampaignSystem.Map.IMapEntity.OnPartyInteraction")]
    [HarmonyPrefix]
    private static void CalculateBaseSpeed2(MobileParty __instance, MobileParty engagingParty)
    {
        if (__instance.IsPartyControlled() || engagingParty.IsPartyControlled())
        {
            if(ModInformation.IsServer)
            {
                ;
            }
        }
    }
}
