using HarmonyLib;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch("DisorganizedStateCampaignBehavior", "RegisterEvents")]
internal class DisableDisorganizedStateCampaignBehavior
{
    static bool Prefix() => false;
}
