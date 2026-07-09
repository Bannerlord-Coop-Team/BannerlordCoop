using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(DisorganizedStateCampaignBehavior))]
internal class DisableDisorganizedStateCampaignBehavior
{
    // Shouldn't need to patch the uses of MobileParty.MainParty for this campaign behavior.
    // Vanilla assumes the campaign map will be paused for the duration of a battle with only one player, 
    // and makes involved parties disorganized at the start of the player MapEvent/battle.
    // Leaving it like this instead means any involved player parties become disorganized only when a MapEvent ends.
    [HarmonyPatch(nameof(DisorganizedStateCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}