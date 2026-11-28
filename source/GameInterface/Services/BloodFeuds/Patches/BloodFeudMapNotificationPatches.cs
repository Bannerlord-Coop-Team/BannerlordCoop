using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;

namespace GameInterface.Services.BloodFeuds.Patches;

[HarmonyPatch]
internal class BloodFeudMapNotificationPatches
{
    // Vanilla removes this notice on HeroKilled, which never fires on clients since the server runs KillCharacterAction
    [HarmonyPatch(typeof(MapNotificationItemBaseVM), nameof(MapNotificationItemBaseVM.ManualRefreshRelevantStatus))]
    [HarmonyPostfix]
    private static void ManualRefreshRelevantStatusPostfix(MapNotificationItemBaseVM __instance)
    {
        if (__instance is BloodFeudClanMemberCapturedMapNotificationItemVM && !__instance.Data.IsValid())
        {
            __instance.ExecuteRemove();
        }
    }

    // A notice re-sent on join can be shown again when the load finishes and replays the saved notices
    [HarmonyPatch(typeof(MapNotificationVM), nameof(MapNotificationVM.AddMapNotification))]
    [HarmonyPrefix]
    private static bool AddMapNotificationPrefix(MapNotificationVM __instance, InformationData data)
    {
        if (data is not BloodFeudClanMemberCapturedMapNotification notice || notice.Settlement == null) return true;

        return !__instance.NotificationItems.Any(item =>
            item.Data is BloodFeudClanMemberCapturedMapNotification shown &&
            shown.CaptiveHero == notice.CaptiveHero &&
            shown.Settlement == notice.Settlement);
    }
}
