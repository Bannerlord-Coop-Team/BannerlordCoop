using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.Core;

namespace GameInterface.Services.BloodFeuds.Patches;

[HarmonyPatch]
internal class BloodFeudMapNotificationPatches
{
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
