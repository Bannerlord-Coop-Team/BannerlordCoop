using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Configuration;

/// <summary>
/// Runs the real Directory.Build.targets against a stub project to pin when a bad BannerlordDir fails the build.
/// </summary>
public class BannerlordDirCheckTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "BannerlordDirCheckTests-" + Guid.NewGuid().ToString("N"));
    private readonly string game;

    /// <summary>Lays out Directory.Build.targets, a stub project with one game reference, and a fake install.</summary>
    public BannerlordDirCheckTests()
    {
        game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "bin", "Win64_Shipping_Client"));
        File.Copy(Path.Combine(StubMsBuild.FindRepoRoot(), "source", "Directory.Build.targets"), Path.Combine(root, "Directory.Build.targets"));
        File.WriteAllText(Path.Combine(root, "stub.proj"),
            "<Project><ItemGroup><Reference Include=\"TaleWorlds.Core\">" +
            "<HintPath>$(BannerlordDir)bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll</HintPath></Reference></ItemGroup>" +
            "<Import Project=\"Directory.Build.targets\" /><Target Name=\"ResolveAssemblyReferences\" />" +
            "<Target Name=\"Build\" DependsOnTargets=\"ResolveAssemblyReferences\" /></Project>");
    }

    /// <summary>Deletes the fake checkout; it holds no links, so a recursive delete stays inside the temp folder.</summary>
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void InstallFolderBuilds()
    {
        var (exitCode, output) = StubMsBuild.Build(root, $"\"-p:BannerlordDir={game}/\"");

        Assert.True(exitCode == 0, output);
    }

    [Fact]
    public void MissingInstallFolderFailsWithOneClearError()
    {
        var (exitCode, output) = StubMsBuild.Build(root, $"\"-p:BannerlordDir={Path.Combine(root, "moved")}/\"");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("isn't a Bannerlord install", output);
    }

    [Fact]
    public void OverrideWithoutTrailingSlashFailsWithOneClearError()
    {
        var (exitCode, output) = StubMsBuild.Build(root, $"\"-p:BannerlordDir={game}\"");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("needs a trailing slash", output);
    }
}
