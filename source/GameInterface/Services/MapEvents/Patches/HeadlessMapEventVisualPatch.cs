using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// <see cref="MapEvent.Initialize"/> creates the map event visual and initializes it without a
/// null check. A host with no map event visual creator (a headless server or test campaign) gets a no-op
/// visual instead of null.
/// </summary>
[HarmonyPatch(typeof(VisualCreator), nameof(VisualCreator.CreateMapEventVisual))]
internal class HeadlessMapEventVisualPatch
{
    [HarmonyPostfix]
    private static void Postfix(VisualCreator __instance, ref IMapEventVisual __result)
    {
        if (__result == null && __instance.MapEventVisualCreator == null)
            __result = HeadlessMapEventVisual.Instance;
    }

    // A map event visual that draws nothing, for a host with no map to draw it on.
    private sealed class HeadlessMapEventVisual : IMapEventVisual
    {
        public static readonly HeadlessMapEventVisual Instance = new HeadlessMapEventVisual();

        public void Initialize(CampaignVec2 position, bool isVisible) { }
        public void OnMapEventEnd() { }
        public void SetVisibility(bool isVisible) { }
    }
}
