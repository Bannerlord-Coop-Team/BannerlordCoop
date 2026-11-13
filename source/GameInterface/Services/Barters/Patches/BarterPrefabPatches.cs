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
        if (movieName != "BarterScreen") return;
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        // Load XML so the barter item prefab edit below takes effect, only for player trades because vanilla barter items dont have the spouse bindings.
        if (trade.IsActive) doNotUseGeneratedPrefabs = true;
    }

    [HarmonyPatch(typeof(WidgetTemplate), nameof(WidgetTemplate.LoadFrom))]
    [HarmonyPrefix]
    public static void LoadTemplatePrefix(XmlNode node)
    {
        if (node.Name != "BarterItemVisualBrushWidget") return;
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        // Other barter screens can also load XML when generated prefabs are unavailable, so only edit for player trades.
        if (!trade.IsActive) return;
        if (!ContainerProvider.TryResolve<IBarterPrefabEditor>(out var editor)) return;

        editor.AddSpousePortrait(node);
    }
}
