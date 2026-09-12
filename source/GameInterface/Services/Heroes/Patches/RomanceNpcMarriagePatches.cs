using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(RomanceCampaignBehavior), nameof(RomanceCampaignBehavior.IsClanSuitableForNpcMarriage))]
internal static class RomanceNpcMarriagePatches
{
    [HarmonyPostfix]
    internal static void IsClanSuitableForNpcMarriagePostfix(Clan clan, ref bool __result)
    {
        if (clan.IsPlayerClan())
            __result = false;
    }
}
