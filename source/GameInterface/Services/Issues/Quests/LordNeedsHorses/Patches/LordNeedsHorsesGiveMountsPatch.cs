using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsHorses.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.GiveMounts))]
internal class LordNeedsHorsesGiveMountsPatch
{
    // The owner's client replays the success after the server moved the mounts, which would remove them a second time
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
