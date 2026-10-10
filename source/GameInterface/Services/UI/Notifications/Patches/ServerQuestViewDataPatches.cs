using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// The server runs quest and issue logs on behalf of players, and nobody reads its unread markers. They are saved,
/// so a player who joins later would get another player's "quest updated" indicator that no quest page can clear.
/// </summary>
[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior))]
internal class ServerQuestViewDataPatches
{
    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnQuestLogAdded))]
    [HarmonyPrefix]
    public static bool OnQuestLogAddedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnIssueLogAdded))]
    [HarmonyPrefix]
    public static bool OnIssueLogAddedPrefix()
    {
        return ModInformation.IsClient;
    }
}
