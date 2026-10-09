using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// The server runs quest and issue outcomes on behalf of players, so vanilla's pop-ups for them are not shown there.
/// </summary>
[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior))]
internal class ServerQuestNotificationPatches
{
    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    public static bool OnIssueUpdatedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnQuestCompleted))]
    [HarmonyPrefix]
    public static bool OnQuestCompletedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnQuestLogAdded))]
    [HarmonyPrefix]
    public static bool OnQuestLogAddedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnQuestStarted))]
    [HarmonyPrefix]
    public static bool OnQuestStartedPrefix()
    {
        return ModInformation.IsClient;
    }
}
