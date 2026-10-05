using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// v1.5 creates the map event visual inside <see cref="MapEvent.Initialize"/> and initializes it without a
/// null check. A host with no map event visual creator (a headless server or test campaign) gets a no-op
/// visual instead of null, which <see cref="MapEventBattleFactory"/> used to pre-assign before Initialize.
/// </summary>
[HarmonyPatch(typeof(VisualCreator), nameof(VisualCreator.CreateMapEventVisual))]
internal class HeadlessMapEventVisualPatch
{
    [HarmonyPostfix]
    private static void Postfix(VisualCreator __instance, ref IMapEventVisual __result)
    {
        if (__result == null && __instance.MapEventVisualCreator == null)
            __result = MapEventBattleFactory.HeadlessMapEventVisual.Instance;
    }
}
