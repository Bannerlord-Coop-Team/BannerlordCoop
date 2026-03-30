using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyVisualManager), "AddNewPartyVisualForParty")]
class DebugFixPatch
{

    [HarmonyPrefix]
    static bool Prefix(MobileParty mobileParty, bool shouldTick)
    {
        Dictionary<PartyBase, MobilePartyVisual> dict = MobilePartyVisualManager.Current._partiesAndVisuals;
        if (dict.ContainsKey(mobileParty.Party))
        {
            return false;
        }
        return true;
    }
}
