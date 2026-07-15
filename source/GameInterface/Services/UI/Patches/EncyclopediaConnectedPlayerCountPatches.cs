using Common;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Adds the live connected-player count to the encyclopedia title.
/// </summary>
[HarmonyPatch(typeof(EncyclopediaNavigatorVM))]
internal static class EncyclopediaConnectedPlayerCountPatches
{
    private static readonly ConditionalWeakTable<EncyclopediaNavigatorVM, Subscription> Subscriptions = new();

    [HarmonyPatch(MethodType.Constructor,
        typeof(Func<string, object, bool, EncyclopediaPageVM>),
        typeof(Action))]
    [HarmonyPostfix]
    private static void ConstructorPostfix(EncyclopediaNavigatorVM __instance)
    {
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<IConnectedPlayerCountService>(out var service)) return;

        Subscriptions.Add(__instance, new Subscription(__instance, service));
    }

    [HarmonyPatch(nameof(EncyclopediaNavigatorVM.OnFinalize))]
    [HarmonyPrefix]
    private static void OnFinalizePrefix(EncyclopediaNavigatorVM __instance)
    {
        if (!Subscriptions.TryGetValue(__instance, out var subscription)) return;

        subscription.Dispose();
        Subscriptions.Remove(__instance);
    }

    [HarmonyPatch(nameof(EncyclopediaNavigatorVM.PageName), MethodType.Setter)]
    [HarmonyPrefix]
    private static void PageNameSetterPrefix(ref string value)
    {
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<IConnectedPlayerCountService>(out var service)) return;

        value = service.FormatEncyclopediaTitle(value);
    }

    /// <summary>Refreshes one navigator until the encyclopedia closes.</summary>
    private sealed class Subscription : IDisposable
    {
        private readonly EncyclopediaNavigatorVM navigator;
        private readonly IConnectedPlayerCountService service;

        public Subscription(
            EncyclopediaNavigatorVM navigator,
            IConnectedPlayerCountService service)
        {
            this.navigator = navigator;
            this.service = service;
            service.ConnectedPlayersChanged += RefreshTitle;
            RefreshTitle();
        }

        private void RefreshTitle()
        {
            navigator.UpdatePageName(string.Empty);
        }

        public void Dispose()
        {
            service.ConnectedPlayersChanged -= RefreshTitle;
        }
    }
}

/// <summary>
/// Reduces the encyclopedia title font so the online player count fits the stock plaque.
/// </summary>
[HarmonyPatch(typeof(GauntletLayer), "LoadMovieAux")]
internal static class EncyclopediaConnectedPlayerCountFontPatch
{
    private const string EncyclopediaMovieName = "EncyclopediaBar";
    private const string TitleBrushName = "Recruitment.Popup.Title.Text";
    private const int ConnectedPlayersTitleFontSize = 36;

    [HarmonyPostfix]
    private static void ReduceTitleFont(IGauntletMovie __result)
    {
        if (ModInformation.IsServer) return;
        if (__result == null || __result.MovieName != EncyclopediaMovieName) return;

        RichTextWidget title = __result.RootWidget.GetFirstInChildrenRecursive(widget =>
            widget is RichTextWidget richText &&
            richText.ReadOnlyBrush.Name?.StartsWith(TitleBrushName, StringComparison.Ordinal) == true) as RichTextWidget;
        if (title == null) return;

        // Vanilla uses 46; 36 keeps "online" clear of the fixed-width plaque's end caps.
        title.Brush.FontSize = ConnectedPlayersTitleFontSize;
    }
}
