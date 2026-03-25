using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAIDisablePatches
{
    [HarmonyPatch(nameof(MobilePartyAi.Tick))]
    [HarmonyPrefix]
    private static bool ClientDisableTickPrefix(MobilePartyAi __instance) => ModInformation.IsServer || __instance._mobileParty.IsPartyControlled();
}
