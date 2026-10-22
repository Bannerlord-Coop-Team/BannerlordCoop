using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// The server runs quest and issue outcomes on behalf of players, and the journal history it writes is saved. A player
/// who joins later would get every other player's finished quests in their Old Quests list, so only clients write it.
/// </summary>
[HarmonyPatch(typeof(JournalLogsCampaignBehavior))]
internal class ServerJournalLogPatches
{
    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnIssueLogAdded))]
    [HarmonyPrefix]
    public static bool OnIssueLogAddedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    public static bool OnIssueUpdatedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestCompleted))]
    [HarmonyPrefix]
    public static bool OnQuestCompletedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestLogAdded))]
    [HarmonyPrefix]
    public static bool OnQuestLogAddedPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.OnQuestStarted))]
    [HarmonyPrefix]
    public static bool OnQuestStartedPrefix()
    {
        return ModInformation.IsClient;
    }
}
