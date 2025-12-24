using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Arenas.Patches;

[HarmonyPatch(typeof(ArenaMasterCampaignBehavior))]
internal class DisableArenaMasterCampaignBehavior
{
    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
