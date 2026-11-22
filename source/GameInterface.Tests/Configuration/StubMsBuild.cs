using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Configuration;

/// <summary>
/// Runs dotnet msbuild on a stub project so tests can exercise the repo's real MSBuild files.
/// </summary>
internal static class StubMsBuild
{
    /// <summary>Builds stub.proj in the given folder and returns the exit code with the combined output.</summary>
    public static (int ExitCode, string Output) Build(string directory, string extraArgument = "")
    {
        var info = new ProcessStartInfo("dotnet", $"msbuild stub.proj -t:Build -nologo -nr:false {extraArgument}")
        {
            WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string variable in new[] { "MSBuildExtensionsPath", "MSBUILD_EXE_PATH", "MSBuildSDKsPath" })
            info.Environment.Remove(variable);

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120000), "dotnet msbuild did not finish within two minutes");
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>Walks up from the test output folder to the checkout that holds Deploy.targets.</summary>
    public static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Deploy.targets"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
