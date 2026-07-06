using GameInterface.Services.Party.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class SetMoveModeHoldPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetMoveModeHold))]
    [HarmonyPrefix]
    static bool BlockHoldDuringCommit()
    {
        if (PartyScreenLogicPatches.InCommit)
            return false;

        return true;
    }
}
