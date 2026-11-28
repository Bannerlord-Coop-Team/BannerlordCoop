using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Barter;

namespace GameInterface.Services.Barters.Patches;

// Vanilla's barter brush only has icons for its own barterables, so ending a blood feud borrows the notification's
[HarmonyPatch(typeof(BarterItemVisualBrushWidget))]
internal class BarterItemVisualBrushWidgetPatches
{
    private const string BloodFeudEndedSprite = "SPGeneral\\MapNotification\\notification_illustration_blood_feud_ended";

    [HarmonyPatch(nameof(BarterItemVisualBrushWidget.UpdateVisual))]
    [HarmonyPostfix]
    private static void UpdateVisualPostfix(BarterItemVisualBrushWidget __instance)
    {
        if (__instance.Type != PlayerPartyBloodFeudBarterable.Id) return;

        var context = __instance.EventManager.Context;
        var sprite = context.SpriteData.GetSprite(BloodFeudEndedSprite);
        if (sprite == null) return;

        // Same as vanilla's fief icons, because the barter brush's default layer is hidden
        __instance.SpriteWidget.Brush = context.DefaultBrush;
        __instance.SetWidgetSpriteForAllStyles(__instance.SpriteWidget, sprite);
    }
}
