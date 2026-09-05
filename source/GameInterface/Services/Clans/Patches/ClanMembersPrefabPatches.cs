using HarmonyLib;
using System.Xml;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanMembersPrefabPatches
{
    [HarmonyPatch(typeof(ScrollablePanelFixedHeaderWidget), nameof(ScrollablePanelFixedHeaderWidget.IsRelevant), MethodType.Setter)]
    [HarmonyPostfix]
    private static void RefreshGroupHeaderPostfix(ScrollablePanelFixedHeaderWidget __instance)
    {
        if (__instance.Id != "PlayersHeader" && __instance.Id != "OtherFamiliesHeader") return;

        // Vanilla stops listening to irrelevant headers, so it misses them becoming relevant again.
        if (__instance.ParentWidget?.ParentWidget?.ParentWidget is ScrollablePanel panel &&
            panel.Id == "ClanElementsScrollablePanel" && panel.InnerPanel != null)
        {
            panel.RefreshFixedHeaders();
        }
    }

    [HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Load))]
    [HarmonyPrefix]
    private static void LoadMoviePrefix(string movieName, ref bool doNotUseGeneratedPrefabs)
    {
        // Generated prefabs cannot bind the additional member collections.
        if (movieName == "ClanScreen") doNotUseGeneratedPrefabs = true;
    }

    [HarmonyPatch(typeof(WidgetTemplate), nameof(WidgetTemplate.LoadFrom))]
    [HarmonyPrefix]
    private static void LoadTemplatePrefix(XmlNode node)
    {
        if (node.Attributes?["Id"]?.Value == "ClanMembersWidget" &&
            ContainerProvider.TryResolve<IClanMembersPrefabEditor>(out var editor))
        {
            editor.AddMemberGroups(node);
        }
    }
}
