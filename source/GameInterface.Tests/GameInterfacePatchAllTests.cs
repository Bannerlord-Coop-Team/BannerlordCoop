using GameInterface.AutoSync;
using HarmonyLib;
using Moq;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Covers the <see cref="GameInterface.PatchAll"/> failure latch and reconnect path with faked install steps.
/// </summary>
public class GameInterfacePatchAllTests
{
    private const string ContributedPatchCategory = "GameInterface.Tests.ContributedPatches";

    private readonly Mock<IGameInterfacePatchInstaller> installer = new();
    private readonly Mock<IAutoSyncPatcher> autoSyncPatcher = new();

    // Reconnect finds the patches still applied, so it only rebinds handlers.
    [Fact]
    public void PatchAll_DoesNotReapplyContributedCategoryOnReconnect()
    {
        Harmony harmony = CreateHarmony(nameof(PatchAll_DoesNotReapplyContributedCategoryOnReconnect));
        var patchCategory = new HarmonyPatchCategoryRegistration(
            typeof(GameInterfacePatchAllTests).Assembly,
            ContributedPatchCategory);

        try
        {
            patchCategory.Apply(harmony);
            AssertContributedPatchCount(harmony, 1);

            CreateGameInterface(harmony, patchCategory).PatchAll();

            AssertContributedPatchCount(harmony, 1);
            installer.Verify(i => i.Install(It.IsAny<Harmony>()), Times.Never);
            autoSyncPatcher.Verify(p => p.RebindHandlers(), Times.Once);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A failure that left patches behind must not be retried as a reconnect.
    [Fact]
    public void PatchAll_AfterAFailedAttempt_RefusesToReportSuccess()
    {
        Harmony harmony = CreateHarmony(nameof(PatchAll_AfterAFailedAttempt_RefusesToReportSuccess));
        var failure = new InvalidOperationException("Install failed");
        installer.Setup(i => i.Install(harmony)).Callback(() => ApplyInstallPatch(harmony)).Throws(failure);

        try
        {
            Exception? firstFailure = Record.Exception(CreateGameInterface(harmony).PatchAll);

            Assert.Same(failure, firstFailure);
            Assert.True(Harmony.HasAnyPatches(harmony.Id));

            AssertRetryRefused(harmony, firstFailure);
            installer.Verify(i => i.Install(harmony), Times.Once);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // The AutoSync step runs inside the latch too.
    [Fact]
    public void PatchAll_AfterAFailedAutoSyncBind_RefusesToReportSuccess()
    {
        Harmony harmony = CreateHarmony(nameof(PatchAll_AfterAFailedAutoSyncBind_RefusesToReportSuccess));
        installer.Setup(i => i.Install(harmony)).Callback(() => ApplyInstallPatch(harmony));
        autoSyncPatcher.Setup(p => p.PatchAll()).Throws(new InvalidOperationException("AutoSync bind failed"));

        try
        {
            Exception? firstFailure = Record.Exception(CreateGameInterface(harmony).PatchAll);

            Assert.Equal("AutoSync bind failed", firstFailure?.Message);

            AssertRetryRefused(harmony, firstFailure);
            installer.Verify(i => i.Install(harmony), Times.Once);
            autoSyncPatcher.Verify(p => p.PatchAll(), Times.Once);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A failure with nothing applied is the same as never having tried.
    [Fact]
    public void PatchAll_AfterAFailureThatLeftNoPatches_RunsTheFullInstallAgain()
    {
        Harmony harmony = CreateHarmony(nameof(PatchAll_AfterAFailureThatLeftNoPatches_RunsTheFullInstallAgain));
        int attempts = 0;
        installer.Setup(i => i.Install(harmony)).Callback(() =>
        {
            if (++attempts == 1) throw new InvalidOperationException("Failed with nothing applied");

            ApplyInstallPatch(harmony);
        });

        try
        {
            Exception? firstFailure = Record.Exception(CreateGameInterface(harmony).PatchAll);

            Assert.Equal("Failed with nothing applied", firstFailure?.Message);
            Assert.False(Harmony.HasAnyPatches(harmony.Id));

            CreateGameInterface(harmony).PatchAll();

            Assert.True(Harmony.HasAnyPatches(harmony.Id));
            installer.Verify(i => i.Install(harmony), Times.Exactly(2));
            autoSyncPatcher.Verify(p => p.PatchAll(), Times.Once);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A later session container gets a new GameInterface over the same harmony id, so retry through a fresh one.
    private void AssertRetryRefused(Harmony harmony, Exception? firstFailure)
    {
        var refused = Assert.Throws<InvalidOperationException>(CreateGameInterface(harmony).PatchAll);

        Assert.Contains("Restart Bannerlord", refused.Message);
        Assert.Same(firstFailure, refused.InnerException);
        autoSyncPatcher.Verify(p => p.RebindHandlers(), Times.Never);
    }

    // Builds the unit under test the way the container does, with the faked install steps.
    private GameInterface CreateGameInterface(Harmony harmony, params HarmonyPatchCategoryRegistration[] patchCategories) =>
        new GameInterface(
            harmony,
            Mock.Of<IAutoSyncPatchCollector>(),
            autoSyncPatcher.Object,
            installer.Object,
            patchCategories);

    // The failure latch is process-wide and keyed by harmony id, so every test needs its own.
    private static Harmony CreateHarmony(string testName) => new($"{testName}.{Guid.NewGuid()}");

    // Stands in for a real install by leaving one patch owned by harmony.
    private static void ApplyInstallPatch(Harmony harmony) =>
        harmony.Patch(
            AccessTools.Method(typeof(GameInterfacePatchAllTests), nameof(InstallPatchTarget)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(GameInterfacePatchAllTests), nameof(NoOpPrefix))));

    // Counts the contributed category's prefixes owned by harmony.
    private static void AssertContributedPatchCount(Harmony harmony, int expected)
    {
        var target = AccessTools.Method(typeof(GameInterfacePatchAllTests), nameof(ContributedPatchTarget));
        var patchInfo = Harmony.GetPatchInfo(target);

        Assert.Equal(expected, patchInfo.Prefixes.Count(patch => patch.owner == harmony.Id));
    }

    private static int patchTargetValue;

    // Target of the faked install patch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int InstallPatchTarget(int value) => patchTargetValue = value;

    // Target of the contributed category patch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ContributedPatchTarget(int value) => patchTargetValue = value;

    // Prefix used by the faked install patch.
    private static void NoOpPrefix()
    {
    }

    /// <summary>
    /// Category patch contributed through a <see cref="HarmonyPatchCategoryRegistration"/>.
    /// </summary>
    [HarmonyPatch(typeof(GameInterfacePatchAllTests), nameof(ContributedPatchTarget))]
    [HarmonyPatchCategory(ContributedPatchCategory)]
    private static class ContributedPatch
    {
        // Does nothing; only its presence is counted.
        [HarmonyPrefix]
        private static void Prefix()
        {
        }
    }
}
