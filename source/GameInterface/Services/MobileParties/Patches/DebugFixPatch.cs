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
    static bool Prefix(PartyBase partyBase)
    {
        Dictionary<PartyBase, MobilePartyVisual> dict = MobilePartyVisualManager.Current._partiesAndVisuals;
        if (dict.ContainsKey(partyBase))
        {
            return false;
        }
        return true;
    }
}
