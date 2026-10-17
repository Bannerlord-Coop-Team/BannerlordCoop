using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch(typeof(LadysKnightOutIssueBehavior), nameof(LadysKnightOutIssueBehavior.OnCheckForIssue))]
internal class LadysKnightOutGenerationPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}
