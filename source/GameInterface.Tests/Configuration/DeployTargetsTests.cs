using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Configuration;

/// <summary>
/// Runs the real Deploy.targets against a stub project to pin which checkouts deploy into the game install.
/// </summary>
public class DeployTargetsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DeployTargetsTests-" + Guid.NewGuid().ToString("N"));
    private readonly string module;

    /// <summary>Lays out a fake checkout holding Deploy.targets and a fake game install with an empty Modules folder.</summary>
    public DeployTargetsTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "deploy"));
        Directory.CreateDirectory(Path.Combine(root, "game", "Modules"));
        File.Copy(Path.Combine(FindRepoRoot(), "Deploy.targets"), Path.Combine(root, "Deploy.targets"));
        File.WriteAllText(Path.Combine(root, "deploy", "SubModule.xml"), "<Module/>");
        File.WriteAllText(Path.Combine(root, "stub.proj"),
            "<Project><PropertyGroup><ModName>Coop</ModName>" +
            $"<BannerlordDir>{root}{Path.DirectorySeparatorChar}game{Path.DirectorySeparatorChar}</BannerlordDir>" +
            $"<TargetDir>{root}{Path.DirectorySeparatorChar}out{Path.DirectorySeparatorChar}</TargetDir></PropertyGroup>" +
            "<Import Project=\"Deploy.targets\" /><Target Name=\"Build\" /></Project>");
        module = Path.Combine(root, "game", "Modules", "Coop");
    }

    /// <summary>Deletes the fake checkout; it holds no links, so a recursive delete stays inside the temp folder.</summary>
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void MainCheckoutDeploys()
    {
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, ".git", "HEAD"), "ref: refs/heads/development");

        Build();

        Assert.True(File.Exists(Path.Combine(module, "SubModule.xml")));
    }

    [Fact]
    public void LinkedWorktreeSkipsDeployAndSaysWhy()
    {
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: elsewhere");

        string output = Build();

        Assert.False(Directory.Exists(module));
        Assert.Contains("Skipping deploy of Coop: linked git worktree", output);
    }

    [Fact]
    public void LinkedWorktreeDeploysWhenOptedIn()
    {
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: elsewhere");

        Build("-p:DeployFromWorktree=true");

        Assert.True(File.Exists(Path.Combine(module, "SubModule.xml")));
    }

    /// <summary>Builds the stub project with dotnet msbuild and returns its output, failing the test on a build error.</summary>
    private string Build(string extraArgument = "")
    {
        var info = new ProcessStartInfo("dotnet", $"msbuild stub.proj -t:Build -nologo -nr:false {extraArgument}")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string variable in new[] { "MSBuildExtensionsPath", "MSBUILD_EXE_PATH", "MSBuildSDKsPath" })
            info.Environment.Remove(variable);

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120000), "dotnet msbuild did not finish within two minutes");
        string output = stdout.Result + stderr.Result;
        Assert.True(process.ExitCode == 0, output);
        return output;
    }

    /// <summary>Walks up from the test output folder to the checkout that holds Deploy.targets.</summary>
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Deploy.targets"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
