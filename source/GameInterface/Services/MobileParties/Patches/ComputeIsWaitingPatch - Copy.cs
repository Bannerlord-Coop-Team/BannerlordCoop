using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
///     Patch that alter <see cref="CampaignSystem.MobileParty.ComputeIsWaiting"/> method so that the player's party is never waiting.
///     <para> For more information see <seealso href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/133">issue #133</seealso></para>
/// </summary>
[HarmonyPatch(typeof(PartyVisualManager), "AddNewPartyVisualForParty")]
class ComputeIsWaitingPatch2
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
