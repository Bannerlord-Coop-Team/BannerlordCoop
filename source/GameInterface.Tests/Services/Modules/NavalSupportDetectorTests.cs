using Common;
using GameInterface.Services.Modules;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Covers the startup naval decision, including the composition CoopMod uses.
/// </summary>
public class NavalSupportDetectorTests
{
    private static readonly string[] BothAssemblies = { ModInformation.MissionsNavalAssemblyFile, ModInformation.CoopNavalAssemblyFile };

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void Detect_NeedsTheModuleTheFlagAndTheAssemblies(bool moduleEnabled, bool flagOn, bool shipped, bool expected)
    {
        var detector = new NavalSupportDetector(Modules(moduleEnabled), new FixedFlag(flagOn), _ => shipped);

        Assert.Equal(expected, detector.Detect());
    }

    // The startup composition must take this build's TESTER flag, so it can't turn naval on where the flag is off.
    [Fact]
    public void ForStartup_WithModuleAndAssemblies_FollowsThisBuildsFeatureFlag()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"naval-detector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string file in BothAssemblies) File.WriteAllBytes(Path.Combine(directory, file), Array.Empty<byte>());

            bool detected = NavalSupportDetector.ForStartup(Modules(true), directory).Detect();

            Assert.Equal(new NavalFeatureFlag().IsEnabled, detected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A module list with or without the War Sails DLC.
    private static IModuleInfoProvider Modules(bool navalEnabled)
    {
        var native = new ModuleInfo("Native", true, false, ApplicationVersion.Empty);
        var naval = new ModuleInfo(ModInformation.NavalDlcModuleId, true, true, ApplicationVersion.Empty);
        return new FixedModules(navalEnabled ? new[] { native, naval } : new[] { native });
    }

    // Returns a fixed module list.
    private sealed class FixedModules : IModuleInfoProvider
    {
        private readonly ModuleInfo[] modules;

        public FixedModules(ModuleInfo[] modules) => this.modules = modules;

        public System.Collections.Generic.IEnumerable<ModuleInfo> GetModuleInfos() => modules.ToArray();
    }

    // A feature flag pinned on or off.
    private sealed class FixedFlag : INavalFeatureFlag
    {
        public FixedFlag(bool isEnabled) => IsEnabled = isEnabled;

        public bool IsEnabled { get; }
    }
}
