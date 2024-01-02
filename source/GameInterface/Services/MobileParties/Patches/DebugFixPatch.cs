using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyVisualManager), "AddNewPartyVisualForParty")]
class DebugFixPatch
{
    private static FieldInfo partiesVisuals => typeof(PartyVisualManager).GetField("_partiesAndVisuals", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPrefix]
    static bool Prefix(PartyBase partyBase)
    {
        Dictionary<PartyBase, PartyVisual> dict = (Dictionary<PartyBase, PartyVisual>)partiesVisuals.GetValue(PartyVisualManager.Current);
        if (dict.ContainsKey(partyBase))
        {
            return false;
        }
        return true;
    }
}
