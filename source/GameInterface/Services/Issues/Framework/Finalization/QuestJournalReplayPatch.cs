using Common;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Vanilla completion callbacks can repeat a line already supplied by the server's snapshot.</summary>
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class QuestJournalReplayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, TextObject text, ref JournalLog __result)
    {
        if (ModInformation.IsServer || !QuestEndingReplay.IsActive || text == null) return true;
        __result = __instance.JournalEntries.FirstOrDefault(log => log.LogText != null && log.LogText.ToString() == text.ToString());
        return __result == null;
    }
}
