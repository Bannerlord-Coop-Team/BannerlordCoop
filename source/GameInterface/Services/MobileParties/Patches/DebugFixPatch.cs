using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyVisualManager), "AddNewPartyVisualForParty")]
class DebugFixPatch
{

    [HarmonyPrefix]
    static bool Prefix(PartyBase partyBase)
    {
        Dictionary<PartyBase, PartyVisual> dict = PartyVisualManager.Current._partiesAndVisuals;
        if (dict.ContainsKey(partyBase))
        {
            return false;
        }
        return true;
    }
}
