using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using HarmonyLib;
using System.Xml;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch]
internal static class BarterPrefabPatches
{
    [HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Load))]
    [HarmonyPrefix]
    public static void LoadMoviePrefix(string movieName, ref bool doNotUseGeneratedPrefabs)
    {
        // Load XML so the barter item prefab edit below takes effect, only for player trades because vanilla barter items dont have the spouse bindings.
        if (movieName == "BarterScreen" && PlayerPartyTradeContext.IsActive) doNotUseGeneratedPrefabs = true;
    }

    [HarmonyPatch(typeof(WidgetTemplate), nameof(WidgetTemplate.LoadFrom))]
    [HarmonyPrefix]
    public static void LoadTemplatePrefix(XmlNode node)
    {
        if (node.Name == "BarterItemVisualBrushWidget" && ContainerProvider.TryResolve<IBarterPrefabEditor>(out var editor))
            editor.AddSpousePortrait(node);
    }
}
