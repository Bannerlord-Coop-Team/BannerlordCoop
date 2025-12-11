using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableIssuesCampaignBehavior
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
