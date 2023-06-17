using HarmonyLib;

namespace GameInterface.Services.Battles.Patches;

[HarmonyPatch("DisorganizedStateCampaignBehavior", "RegisterEvents")]
internal class DisableDisorganizedStateCampaignBehavior
{
    static bool Prefix() => false;
}
