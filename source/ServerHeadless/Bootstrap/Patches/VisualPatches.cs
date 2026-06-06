using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Map-event visuals are created by an <c>IMapEventVisualCreator</c> installed by the UI/scene
    /// layer, which we don't run headless — so <see cref="VisualCreator.CreateMapEventVisual"/>
    /// returns null and callers NRE (on load of a save with an in-progress battle, and whenever a
    /// battle map event is created during ticking). Return a no-op visual instead.
    /// </summary>
    [HarmonyPatch(typeof(VisualCreator), nameof(VisualCreator.CreateMapEventVisual))]
    internal class VisualPatches
    {
        static bool Prefix(ref IMapEventVisual __result)
        {
            __result = HeadlessMapEventVisual.Instance;
            return false;
        }
    }

    /// <summary>No-op <see cref="IMapEventVisual"/> for headless operation (no scene to render to).</summary>
    internal sealed class HeadlessMapEventVisual : IMapEventVisual
    {
        public static readonly HeadlessMapEventVisual Instance = new HeadlessMapEventVisual();

        public void Initialize(CampaignVec2 position, bool isVisible) { }
        public void OnMapEventEnd() { }
        public void SetVisibility(bool isVisible) { }
    }
}
