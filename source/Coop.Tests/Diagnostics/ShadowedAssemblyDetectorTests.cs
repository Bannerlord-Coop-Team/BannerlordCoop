using Coop.Core.Diagnostics;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Diagnostics;

public class ShadowedAssemblyDetectorTests : IDisposable
{
    private readonly string appBaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ShadowedAssemblyDetector detector = new ShadowedAssemblyDetector();

    public ShadowedAssemblyDetectorTests()
    {
        Directory.CreateDirectory(appBaseDirectory);
        File.WriteAllText(Path.Combine(appBaseDirectory, "TaleWorlds.Library.dll"), string.Empty);
    }

    public void Dispose()
    {
        Directory.Delete(appBaseDirectory, true);
    }

    [Theory]
    [InlineData("Common.dll")]
    [InlineData("0Harmony.dll")]
    public void FindsForeignCopyInAppBase(string fileName)
    {
        var foreignPath = Path.Combine(appBaseDirectory, fileName);
        File.WriteAllText(foreignPath, string.Empty);

        var shadowingFiles = detector.FindShadowingFiles(appBaseDirectory);

        Assert.Equal(foreignPath, Assert.Single(shadowingFiles));
    }

    [Fact]
    public void FindsNothingWhenAppBaseHasNoForeignCopy()
    {
        Assert.Empty(detector.FindShadowingFiles(appBaseDirectory));
    }
}
