using Coop.Core.Diagnostics;
using HarmonyLib;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Diagnostics;

public class ShadowedAssemblyDetectorTests : IDisposable
{
    private readonly string appBaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly string harmonyPath;
    private readonly Version harmonyVersion = typeof(Harmony).Assembly.GetName().Version;

    public ShadowedAssemblyDetectorTests()
    {
        Directory.CreateDirectory(appBaseDirectory);
        harmonyPath = Path.Combine(appBaseDirectory, "0Harmony.dll");
    }

    public void Dispose()
    {
        Directory.Delete(appBaseDirectory, true);
    }

    [Fact]
    public void FindsForeignCommonCopy()
    {
        var commonPath = Path.Combine(appBaseDirectory, "Common.dll");
        File.WriteAllText(commonPath, string.Empty);

        var shadowingFiles = new ShadowedAssemblyDetector().FindShadowingFiles(appBaseDirectory);

        Assert.Equal(commonPath, Assert.Single(shadowingFiles));
    }

    [Fact]
    public void FindsHarmonyOlderThanCoops()
    {
        CopyRealHarmony();
        var detector = new ShadowedAssemblyDetector(new Version(harmonyVersion.Major + 1, 0, 0, 0));

        var shadowingFiles = detector.FindShadowingFiles(appBaseDirectory);

        Assert.Equal(harmonyPath, Assert.Single(shadowingFiles));
    }

    // Pins the default minimum to Coop's own 0Harmony reference, any old managed dll stands in for Harmony 2.2
    [Fact]
    public void FindsHarmonyOlderThanCoopsReference()
    {
        File.Copy(typeof(ShadowedAssemblyDetectorTests).Assembly.Location, harmonyPath);

        var shadowingFiles = new ShadowedAssemblyDetector().FindShadowingFiles(appBaseDirectory);

        Assert.Equal(harmonyPath, Assert.Single(shadowingFiles));
    }

    [Fact]
    public void FindsUnreadableHarmony()
    {
        File.WriteAllText(harmonyPath, string.Empty);

        var shadowingFiles = new ShadowedAssemblyDetector().FindShadowingFiles(appBaseDirectory);

        Assert.Equal(harmonyPath, Assert.Single(shadowingFiles));
    }

    [Fact]
    public void KeepsHarmonyCoopReferences()
    {
        CopyRealHarmony();

        Assert.Empty(new ShadowedAssemblyDetector().FindShadowingFiles(appBaseDirectory));
    }

    [Fact]
    public void KeepsNewerHarmony()
    {
        CopyRealHarmony();
        var detector = new ShadowedAssemblyDetector(new Version(harmonyVersion.Major, 0, 0, 0));

        Assert.Empty(detector.FindShadowingFiles(appBaseDirectory));
    }

    [Fact]
    public void FindsNothingWhenAppBaseHasNoForeignCopy()
    {
        Assert.Empty(new ShadowedAssemblyDetector().FindShadowingFiles(appBaseDirectory));
    }

    private void CopyRealHarmony()
    {
        File.Copy(typeof(Harmony).Assembly.Location, harmonyPath);
    }
}
