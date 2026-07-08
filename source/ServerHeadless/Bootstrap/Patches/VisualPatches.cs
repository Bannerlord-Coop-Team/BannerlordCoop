using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Headless, the UI layer never installs an <c>IMapEventVisualCreator</c>, so
    /// <see cref="VisualCreator.CreateMapEventVisual"/> would return null and callers NRE.
    /// Construct the REAL <see cref="GauntletMapEventVisual"/> (with no UI delegates): its
    /// constructor is what the Coop mod's GauntletMapEventVisualRegistry patches to replicate
    /// battle/raid map icons to clients, and the <c>MapEvent.MapEventVisual</c> field sync can
    /// only resolve ids for instances of that registered type. A private no-op stub here meant
    /// clients never saw map-event icons from a headless server ("Failed to get id for ...
    /// HeadlessMapEventVisual"). The class is safe headless: its constructor is pure managed,
    /// and the ambient-sound path disables itself because <see cref="SoundManagerPatches"/>
    /// resolves every sound index to -1.
    /// </summary>
    [HarmonyPatch(typeof(VisualCreator), nameof(VisualCreator.CreateMapEventVisual))]
    internal class VisualPatches
    {
        static bool Prefix(MapEvent __0, ref IMapEventVisual __result)
        {
            __result = new GauntletMapEventVisual(__0, null, null, null);
            return false;
        }
    }

    /// <summary>
    /// <see cref="TaleWorlds.Engine.SoundManager.GetEventGlobalIndex"/> is a native call (dead
    /// headless) and runs inside type initializers (e.g. GauntletMapEventVisual's cctor resolves
    /// its ambient battle sounds there) — an exception would poison those types. Return -1
    /// ("no such sound"), which also self-disables every sound-playback branch keyed on it.
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.Engine.SoundManager), nameof(TaleWorlds.Engine.SoundManager.GetEventGlobalIndex))]
    internal class SoundManagerPatches
    {
        static bool Prefix(ref int __result)
        {
            __result = -1;
            return false;
        }
    }

    /// <summary>
    /// <see cref="MobilePartyVisualManager.Current"/> dereferences the SandBox.View submodule's
    /// visual manager, which isn't loaded headless. Return null — callers (e.g.
    /// PartyVisualRegistry.RegisterAllObjects) null-check and skip visual registration.
    /// </summary>
    [HarmonyPatch(typeof(MobilePartyVisualManager), nameof(MobilePartyVisualManager.Current), MethodType.Getter)]
    internal class MobilePartyVisualManagerPatches
    {
        static bool Prefix(ref MobilePartyVisualManager __result)
        {
            __result = null;
            return false;
        }
    }
}
