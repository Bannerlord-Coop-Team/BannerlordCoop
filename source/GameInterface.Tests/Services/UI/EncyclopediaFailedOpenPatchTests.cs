using System;
using Common.Util;
using GameInterface.Services.UI.Patches;
using HarmonyLib;
using SandBox.GauntletUI.Encyclopedia;
using TaleWorlds.Engine.GauntletUI;
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
}
