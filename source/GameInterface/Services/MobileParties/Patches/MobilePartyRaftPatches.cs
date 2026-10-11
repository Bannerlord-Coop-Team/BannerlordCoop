using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyRaftPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsInRaftState), MethodType.Setter)]
    [HarmonyPostfix]
    public static void IsInRaftStateSetterPostfix(MobileParty __instance)
    {
        // Fire event on client to open raft state menu
        if (ModInformation.IsClient)
        {
            CampaignEventDispatcher.Instance.OnMobilePartyRaftStateChanged(__instance);
        }
    }
}
