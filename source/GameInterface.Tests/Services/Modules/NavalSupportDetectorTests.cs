using Common;
using GameInterface.Services.Modules;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Covers the startup naval decision with the hosting side's warSails flag, including the composition CoopMod uses.
/// </summary>
[Collection("ModConfigSerial")]
public class NavalSupportDetectorTests
{
    private static readonly string[] BothAssemblies = { ModInformation.MissionsNavalAssemblyFile, ModInformation.CoopNavalAssemblyFile };

    // A client ignores its own warSails, so the flag value changes nothing and never refuses.
    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(false, false, false, false, false)]
    public void Decide_OnAClient_NeedsTheModuleTheFlagTheNavalBuildAndTheAssemblies(
        bool moduleEnabled, bool flagOn, bool navalBuilt, bool shipped, bool expected)
    {
        foreach (bool warSails in new[] { false, true })
        {
            var decision = Detector(moduleEnabled, flagOn, navalBuilt, shipped, warSails, isHost: false).Decide();

            Assert.Equal(expected, decision.IsActive);
            Assert.Null(decision.HostRefusal);
        }
    }

    // Naval assemblies left by an earlier naval deploy must not turn naval on in a build made with CoopNavalDlc=false.
    [Fact]
    public void Decide_BuiltWithoutNavalCode_StaysOffEvenWithStaleNavalAssemblies()
    {
        var decision = Detector(true, flagOn: true, navalBuilt: false, shipped: true, warSails: false, isHost: false).Decide();

        Assert.False(decision.IsActive);
    }

    // The hosting side runs War Sails only when warSails, NavalDLC and the build all agree, and refuses any mismatch.
    [Theory]
    [InlineData(false, false, true, true, true, false, null)]
    [InlineData(true, true, true, true, true, true, null)]
    [InlineData(false, true, true, true, true, false, "Disable NavalDLC in the launcher, or set warSails to true")]
    [InlineData(false, true, false, false, false, false, "Disable NavalDLC in the launcher, or set warSails to true")]
    [InlineData(true, false, true, true, true, false, "NavalDLC is not enabled")]
    [InlineData(true, true, false, true, true, false, "War Sails co-op is only in tester builds")]
    [InlineData(true, true, true, false, true, false, "this build was made without CoopNavalDlc")]
    [InlineData(true, true, true, true, false, false, "this build has no naval code")]
    public void Decide_OnTheHost_FollowsWarSailsAndRefusesAMismatch(
        bool warSails, bool moduleEnabled, bool flagOn, bool navalBuilt, bool shipped, bool expectedActive, string refusal)
    {
        var decision = Detector(moduleEnabled, flagOn, navalBuilt, shipped, warSails, isHost: true).Decide();

        Assert.Equal(expectedActive, decision.IsActive);
        if (refusal == null)
        {
            Assert.Null(decision.HostRefusal);
            return;
        }
        Assert.Contains(refusal, decision.HostRefusal);
        Assert.True(decision.RefusesToHost);
    }

    // The startup composition must take this build's TESTER and CoopNavalDlc flags, so it can't turn naval on where either is off.
    [Fact]
    public void ForStartup_OnAClient_FollowsThisBuildsFlags()
    {
        string directory = AssemblyDirectory();
        try
        {
            bool detected = NavalSupportDetector.ForStartup(Modules(true), directory, isHost: false).Decide().IsActive;

            var flag = new NavalFeatureFlag();
            Assert.Equal(flag.IsEnabled && flag.IsNavalCodeBuilt, detected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A host reads warSails from the mod-config.json in COOP_DATA_DIR, the folder the dedicated server sets.
    [Fact]
    public void ForStartup_OnTheHost_ReadsWarSailsFromModConfig()
    {
        string directory = AssemblyDirectory();
        string saved = Environment.GetEnvironmentVariable("COOP_DATA_DIR");
        File.WriteAllText(Path.Combine(directory, "mod-config.json"), "{ \"modOptions\": { \"warSails\": false } }");
        Environment.SetEnvironmentVariable("COOP_DATA_DIR", directory);
        try
        {
            var decision = NavalSupportDetector.ForStartup(Modules(true), directory, isHost: true).Decide();

            Assert.False(decision.IsActive);
            Assert.Contains("set warSails to true", decision.HostRefusal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COOP_DATA_DIR", saved);
            Directory.Delete(directory, recursive: true);
        }
    }

    // A temp folder holding both naval assemblies.
    private static string AssemblyDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"naval-detector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (string file in BothAssemblies) File.WriteAllBytes(Path.Combine(directory, file), Array.Empty<byte>());
        return directory;
    }

    // A detector over pinned inputs.
    private static NavalSupportDetector Detector(bool moduleEnabled, bool flagOn, bool navalBuilt, bool shipped, bool warSails, bool isHost)
    {
        return new NavalSupportDetector(Modules(moduleEnabled), new FixedFlag(flagOn, navalBuilt), _ => shipped,
            new FixedWarSails(warSails), isHost);
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

    // A feature flag pinned on or off, built with or without the naval code.
    private sealed class FixedFlag : INavalFeatureFlag
    {
        // Pins both flag values for one test case.
        public FixedFlag(bool isEnabled, bool isNavalCodeBuilt = true)
        {
            IsEnabled = isEnabled;
            IsNavalCodeBuilt = isNavalCodeBuilt;
        }

        public bool IsEnabled { get; }

        public bool IsNavalCodeBuilt { get; }
    }

    // A warSails value pinned on or off.
    private sealed class FixedWarSails : IWarSailsSetting
    {
        // Pins the flag for one test case.
        public FixedWarSails(bool isEnabled) => IsEnabled = isEnabled;

        public bool IsEnabled { get; }
    }
}
