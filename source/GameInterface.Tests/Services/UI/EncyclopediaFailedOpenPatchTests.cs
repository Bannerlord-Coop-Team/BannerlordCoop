using System;
using Common.Util;
using GameInterface.Services.UI.Patches;
using HarmonyLib;
using SandBox.GauntletUI.Encyclopedia;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Regression coverage for the encyclopedia that failed to open (issue #2470).</summary>
public class EncyclopediaFailedOpenPatchTests
{
    [Fact]
    public void Finalizer_PageMovieMissing_DetachesEncyclopediaData()
    {
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        // A null _activeGauntletMovie is what SetEncyclopediaPage leaves behind when the page view
        // model throws: the layer exists, but no page movie was ever loaded.
        view._encyclopediaData = ObjectHelper.SkipConstructor<EncyclopediaData>();
        view.IsEncyclopediaOpen = true;

        var result = EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(
            view,
            new InvalidOperationException("page view model threw"));

        Assert.Null(result);
        Assert.Null(view._encyclopediaData);
        Assert.False(view.IsEncyclopediaOpen);
    }

    [Fact]
    public void Finalizer_CloseThrows_StillDetachesEncyclopediaData()
    {
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        // An uninitialised layer stands in for the engine state that made the vanilla close throw.
        // Detaching the view is what stops the retry loop, so it cannot depend on that close.
        data._activeGauntletLayer = ObjectHelper.SkipConstructor<GauntletLayer>();
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;

        EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, new InvalidOperationException("boom"));

        Assert.Null(view._encyclopediaData);
        Assert.False(view.IsEncyclopediaOpen);
    }

    [Fact]
    public void Finalizer_PageMovieLoaded_KeepsWorkingEncyclopediaAndRethrows()
    {
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        data._activeGauntletMovie = ObjectHelper.SkipConstructor<GauntletMovieIdentifier>();
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;
        var thrown = new InvalidOperationException("navigation failed");

        var result = EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, thrown);

        Assert.Same(thrown, result);
        Assert.Same(data, view._encyclopediaData);
        Assert.True(view.IsEncyclopediaOpen);
    }

    [Fact]
    public void Finalizer_NoException_LeavesEncyclopediaAlone()
    {
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;

        var result = EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, null);

        Assert.Null(result);
        Assert.Same(data, view._encyclopediaData);
        Assert.True(view.IsEncyclopediaOpen);
    }

    [Fact]
    public void Finalizer_IsAttachedToExecuteLink()
    {
        var harmony = new Harmony(nameof(Finalizer_IsAttachedToExecuteLink));
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(EncyclopediaFailedOpenPatches)).Patch();

            Assert.Contains(patched, method => method.Name.Contains("ExecuteLink"));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ReleaseMovieGuard_IsAttachedToReleaseMovie()
    {
        var harmony = new Harmony(nameof(ReleaseMovieGuard_IsAttachedToReleaseMovie));
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(ReleaseMissingMovieRobustnessPatch)).Patch();

            Assert.Contains(patched, method => method.Name.Contains(nameof(GauntletLayer.ReleaseMovie)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ReleaseMovie_MissingIdentifier_SkipsOriginal()
    {
        Assert.False(ReleaseMissingMovieRobustnessPatch.SkipMissingIdentifier(null));
    }

    [Fact]
    public void ReleaseMovie_RealIdentifier_RunsOriginal()
    {
        var identifier = ObjectHelper.SkipConstructor<GauntletMovieIdentifier>();

        Assert.True(ReleaseMissingMovieRobustnessPatch.SkipMissingIdentifier(identifier));
    }

    [Fact]
    public void Finalizer_CloseThrows_ReleasesFocusHeldByFailedLayer()
    {
        var layer = CreateFocusableLayer();
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        data._activeGauntletLayer = layer;
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;

        try
        {
            ScreenManager.FocusedLayer = layer;

            EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, new InvalidOperationException("boom"));

            // RemoveLayer never clears ScreenManager.FocusedLayer, so a focused layer the
            // containment destroys would be dereferenced by the next TrySetFocus (issue #2470's
            // follow-up defect). The containment must release the focus it is about to orphan,
            // even when the vanilla close throws.
            Assert.Null(ScreenManager.FocusedLayer);
        }
        finally
        {
            ScreenManager.FocusedLayer = null;
        }
    }

    [Fact]
    public void Finalizer_LayerDiesDuringClose_ReleasesFocusWithoutThrowing()
    {
        var layer = CreateFocusableLayer();
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        data._activeGauntletLayer = layer;
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;

        // Stand-in for the close succeeding: HandleFinalize's observable effect on the focus path
        // is a null UIContext, which is exactly what made the in-game follow-up NRE. Releasing
        // focus only after the close would dereference that null and fail this test.
        var harmony = new Harmony(nameof(Finalizer_LayerDiesDuringClose_ReleasesFocusWithoutThrowing));
        try
        {
            _layerFinalizedByClose = layer;
            harmony.Patch(
                AccessTools.Method(typeof(GauntletMapEncyclopediaView), nameof(GauntletMapEncyclopediaView.CloseEncyclopedia)),
                prefix: new HarmonyMethod(typeof(EncyclopediaFailedOpenPatchTests), nameof(FinalizeLayerInsteadOfClosing)));
            ScreenManager.FocusedLayer = layer;

            var result = EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, new InvalidOperationException("boom"));

            Assert.Null(result);
            Assert.Null(ScreenManager.FocusedLayer);
        }
        finally
        {
            ScreenManager.FocusedLayer = null;
            _layerFinalizedByClose = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void Finalizer_FocusHeldByOtherLayer_LeavesFocusAlone()
    {
        var layer = CreateFocusableLayer();
        var otherLayer = ObjectHelper.SkipConstructor<GauntletLayer>();
        var view = ObjectHelper.SkipConstructor<GauntletMapEncyclopediaView>();
        var data = ObjectHelper.SkipConstructor<EncyclopediaData>();
        data._activeGauntletLayer = layer;
        view._encyclopediaData = data;
        view.IsEncyclopediaOpen = true;

        try
        {
            ScreenManager.FocusedLayer = otherLayer;

            EncyclopediaFailedOpenPatches.Finalizer_ExecuteLink(view, new InvalidOperationException("boom"));

            // A healthy layer's focus is not the containment's to take; only focus held by the
            // layer being destroyed may be released.
            Assert.Same(otherLayer, ScreenManager.FocusedLayer);
        }
        finally
        {
            ScreenManager.FocusedLayer = null;
        }
    }

    private static GauntletLayer? _layerFinalizedByClose;

    private static bool FinalizeLayerInsteadOfClosing()
    {
        _layerFinalizedByClose!.UIContext = null;
        return false;
    }

    // The minimum live state HandleLoseFocus touches: a key list to clear and an event manager to
    // drop widget focus on. Both no-op safely on otherwise uninitialised objects.
    private static GauntletLayer CreateFocusableLayer()
    {
        var layer = ObjectHelper.SkipConstructor<GauntletLayer>();
        layer.Input = new InputContext();
        layer.UIContext = ObjectHelper.SkipConstructor<UIContext>();
        layer.UIContext.EventManager = ObjectHelper.SkipConstructor<EventManager>();
        return layer;
    }
}
