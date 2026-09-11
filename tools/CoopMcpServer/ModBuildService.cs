using System.Diagnostics;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CoopMcpServer.Tests")]

namespace CoopMcpServer;

public interface IModBuildService
{
    Task BuildAsync(string repository, DeploymentSettings settings, string artifacts, CancellationToken cancellationToken, string configuration = "Release");
}

public sealed class ModBuildService : IModBuildService
{
    private readonly IBuildProcessRunner runner;
    public ModBuildService(IBuildProcessRunner runner) { this.runner = runner; }

    public async Task BuildAsync(string repository, DeploymentSettings settings, string artifacts, CancellationToken cancellationToken, string configuration = "Release")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Deployment builds require Windows MSBuild.");
        var projects = new List<string> { "Coop" };
        if (File.Exists(Path.Combine(repository, "source", "Missions.Naval", "Missions.Naval.csproj"))) projects.Add("Missions.Naval");
        foreach (string project in projects)
        {
            var info = CreateStartInfo(repository, settings.MsbuildPath, artifacts, project, configuration);
            File.WriteAllText(Path.Combine(artifacts, project + "-command.json"), JsonSerializer.Serialize(new { info.FileName, Arguments = info.ArgumentList.ToArray(), info.WorkingDirectory }));
            await RunBuildAsync(info, artifacts, project, cancellationToken);
        }
    }

    internal Task RunBuildAsync(ProcessStartInfo info, string artifacts, string project, CancellationToken cancellationToken) =>
        runner.RunAsync(info, artifacts, project, cancellationToken);

    public static ProcessStartInfo CreateStartInfo(string repository, string msbuild, string artifacts, string project, string configuration = "Release")
    {
        if (configuration != "Release" && configuration != "Debug") throw new ArgumentException("configuration must be Release or Debug.", nameof(configuration));
        if (project != "Coop" && project != "Missions.Naval") throw new ArgumentException("Unsupported deployment project.");
        var info = new ProcessStartInfo(msbuild)
        {
            WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        string scratch = Path.Combine(artifacts, "disabled-deploy");
        // Rebuild the optional plugin after shared references, without rebuilding those references again.
        foreach (string argument in new[] { Path.Combine(repository, "source", project, project + ".csproj"),
            "-t:Rebuild", "-restore", "-p:RestorePackagesConfig=true", "-nr:false", "-m:1", "-nologo",
            "-p:Configuration=" + configuration, "-p:Platform=AnyCPU", "-p:ModName=", "-p:PreBuildEvent=", "-p:PostBuildEvent=",
            "-p:ModsRoot=" + EscapeProperty(scratch), "-p:ModDir=" + EscapeProperty(Path.Combine(scratch, "Coop")),
            "-p:ModBinDir=" + EscapeProperty(Path.Combine(scratch, "Coop", "bin")), "-p:ModPrefabDir=" + EscapeProperty(Path.Combine(scratch, "Coop", "prefabs")) })
            info.ArgumentList.Add(argument);
        if (project == "Missions.Naval") info.ArgumentList.Add("-p:BuildProjectReferences=false");
        return info;
    }

    private static string EscapeProperty(string value)
    {
        // ArgumentList handles Windows quoting, but MSBuild also parses property separators/expansions.
        foreach (char character in new[] { '%', ';', '$', '@', '\'', '(', ')', '*', '?' })
            value = value.Replace(character.ToString(), "%" + ((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        return value;
    }
}
