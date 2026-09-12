using System.Xml.Linq;

namespace CoopMcpServer;

public interface IDeploymentPlan
{
    DeploymentLayout Validate(string solution, LaunchProfile profile, string configuration = "Release");
    IReadOnlyList<DeploymentInput> Collect(DeploymentLayout layout, LaunchProfile profile);
}

public sealed record DeploymentLayout(string Solution, string Repository, string Module, string DurableRoot, bool Naval, string Configuration = "Release");
public sealed record DeploymentInput(string Source, string Target);

public sealed class DeploymentPlan : IDeploymentPlan
{
    private readonly IDeploymentPaths paths;
    private readonly IDeploymentFiles files;
    public DeploymentPlan(IDeploymentPaths paths, IDeploymentFiles files) { this.paths = paths; this.files = files; }

    private static readonly string[] Projects = { "Coop", "Common", "Coop.Core", "GameInterface", "Missions", "Coop.Steam", "Coop.CrashReporter" };
    // Only runtime dependencies of the mod, never game assemblies or recursive publicizer output.
    private static readonly string[] Dependencies = ("0Harmony Autofac Concentus DiscordRPC Fmod5Sharp K4os.Compression.LZ4 LiteNetLib " +
        "Microsoft.Bcl.AsyncInterfaces Microsoft.CodeAnalysis Microsoft.CodeAnalysis.CSharp Microsoft.Extensions.DependencyInjection.Abstractions " +
        "Microsoft.Extensions.Primitives Mono.Cecil Mono.Cecil.Mdb Mono.Cecil.Pdb Mono.Cecil.Rocks MonoMod.Backports MonoMod.Core MonoMod.Iced " +
        "MonoMod.ILHelpers MonoMod.RuntimeDetour MonoMod.Utils NAudio.Core NAudio.WinMM Newtonsoft.Json protobuf-net protobuf-net.Core " +
        "Scriban Scrutor Serilog Serilog.Enrichers.Process Serilog.Sinks.Debug Serilog.Sinks.File Serilog.Sinks.Seq " +
        "System.Buffers System.Collections.Immutable System.Diagnostics.DiagnosticSource System.IO.Pipelines System.Memory " +
        "System.Numerics.Vectors System.Reflection.Metadata System.Reflection.Primitives System.Runtime.CompilerServices.Unsafe " +
        "System.Text.Encodings.Web System.Text.Json System.Threading.Channels System.Threading.Tasks.Extensions System.ValueTuple").Split(' ');

    public DeploymentLayout Validate(string solution, LaunchProfile profile, string configuration = "Release")
    {
        if (configuration != "Release" && configuration != "Debug") throw new ArgumentException("configuration must be Release or Debug.", nameof(configuration));
        profile.Validate(0);
        var config = profile.Deployment;
        if (config == null) throw new ArgumentException("Deployment is not enabled for this profile.");
        if (!Path.IsPathFullyQualified(solution ?? "") || Path.GetFileName(solution) != "Coop.sln")
            throw new ArgumentException("solution_path must be an absolute source/Coop.sln path.");
        solution = Path.GetFullPath(solution);
        paths.NoLinks(solution);
        string source = Path.GetDirectoryName(solution);
        string repository = Path.GetDirectoryName(source);
        if (Path.GetFileName(source) != "source" || !File.Exists(solution) ||
            !File.Exists(Path.Combine(repository, "Deploy.targets")) || !File.Exists(Path.Combine(repository, "deploy", "SubModule.xml")) ||
            !Directory.Exists(Path.Combine(repository, "UIMovies")) ||
            (!File.Exists(Path.Combine(repository, ".git")) && !Directory.Exists(Path.Combine(repository, ".git"))))
            throw new ArgumentException("Expected a trusted BannerlordCoop checkout with source/Coop.sln, Deploy.targets, deploy and UIMovies.");
        string text = File.ReadAllText(solution);
        if (!text.TrimStart().StartsWith("Microsoft Visual Studio Solution File, Format Version 12.00", StringComparison.Ordinal))
            throw new ArgumentException("Expected a Visual Studio Coop.sln solution, not an arbitrary project file.");
        bool naval = Directory.Exists(Path.Combine(source, "Missions.Naval"));
        foreach (string project in Projects.Concat(naval ? new[] { "Missions.Naval" } : Array.Empty<string>()))
        {
            string path = Path.Combine(source, project, project + ".csproj");
            paths.NoLinks(path);
            if (!File.Exists(path) || !text.Contains($"\"{project}\\{project}.csproj\"", StringComparison.Ordinal))
                throw new ArgumentException("Solution is missing the expected project: " + project);
            if (XDocument.Load(path).Root?.Name.LocalName != "Project") throw new ArgumentException("Invalid project XML: " + path);
        }
        string exe = Path.GetFullPath(profile.Executable);
        string bin = Path.GetDirectoryName(exe);
        if (!string.Equals(Path.GetFileName(bin), "Win64_Shipping_Client", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(Path.GetDirectoryName(bin)), "bin", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Profile executable must be in the installation's bin/Win64_Shipping_Client directory.");
        string game = Path.GetDirectoryName(Path.GetDirectoryName(bin));
        string module = Path.Combine(game, "Modules", "Coop");
        paths.NoLinks(module);
        if (!File.Exists(Path.Combine(game, "Modules", "Native", "SubModule.xml")) || !Directory.Exists(module))
            throw new ArgumentException("Profile requires an existing Bannerlord installation and Modules/Coop directory.");
        string durable = paths.DurableRoot(config.DurableRoot);
        if (paths.Within(durable, game) || paths.Within(game, durable) ||
            paths.Within(durable, repository) || paths.Within(repository, durable) ||
            string.Equals(durable, game, StringComparison.OrdinalIgnoreCase) || string.Equals(durable, repository, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("durableRoot must be separate from the checkout and game installation.");
        if (!Path.IsPathFullyQualified(config.MsbuildPath ?? "") || !File.Exists(config.MsbuildPath) ||
            !string.Equals(Path.GetFileName(config.MsbuildPath), "MSBuild.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("deployment.msbuildPath must identify an existing absolute Windows MSBuild.exe.");
        if (config.BuildSpaceBytes < 1024L * 1024 * 1024 || config.BuildSpaceBytes > 100L * 1024 * 1024 * 1024 ||
            config.DiskReserveBytes < 256L * 1024 * 1024 || config.DiskReserveBytes > 100L * 1024 * 1024 * 1024)
            throw new ArgumentException("Build space must be 1..100 GiB and reserve 256 MiB..100 GiB.");
        string xml = config.SubModuleXml ?? Path.Combine(module, "SubModule.xml");
        if (!Path.IsPathFullyQualified(xml)) throw new ArgumentException("subModuleXml must be absolute.");
        ValidateXml(xml);
        return new DeploymentLayout(solution, repository, module, durable, naval, configuration);
    }

    public IReadOnlyList<DeploymentInput> Collect(DeploymentLayout layout, LaunchProfile profile)
    {
        var inputs = new List<DeploymentInput>();
        string output = Path.Combine(layout.Repository, "source", "Coop", "bin", layout.Configuration);
        string bin = Path.Combine(layout.Module, "bin", "Win64_Shipping_Client");
        foreach (string project in Projects.Concat(layout.Naval ? new[] { "Missions.Naval" } : Array.Empty<string>()))
        {
            string extension = project == "Coop.CrashReporter" ? ".exe" : ".dll";
            string projectOutput = project is "Coop" or "Coop.CrashReporter" ? Path.Combine(layout.Repository, "source", project, "bin", layout.Configuration) :
                Path.Combine(layout.Repository, "source", project, "bin", layout.Configuration, "netstandard2.0");
            string file = Path.Combine(projectOutput, project + extension);
            if (!File.Exists(file)) throw new FileNotFoundException($"Required {layout.Configuration} build output missing.", file);
            if (project != "Missions.Naval")
            {
                string referenced = Path.Combine(output, project + extension);
                if (!File.Exists(referenced) || files.Inspect(referenced) != files.Inspect(file))
                    throw new InvalidDataException("Coop output disagrees with the freshly built project: " + project);
            }
            inputs.Add(new(file, Path.Combine(bin, project + extension)));
        }
        foreach (string file in Directory.EnumerateFiles(output, "*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (Projects.Contains(name) || name == "Missions.Naval" || Dependencies.Contains(name) ||
                name.StartsWith("TaleWorlds.", StringComparison.Ordinal) || name.StartsWith("SandBox", StringComparison.Ordinal) ||
                name.StartsWith("StoryMode", StringComparison.Ordinal) || name == "Steamworks.NET" || name == "netstandard") continue;
            throw new InvalidDataException("Unrecognized top-level build DLL; update the runtime allowlist deliberately: " + file);
        }
        foreach (string dependency in Dependencies)
        {
            string file = Path.Combine(output, dependency + ".dll");
            if (File.Exists(file)) inputs.Add(new(file, Path.Combine(bin, dependency + ".dll")));
        }
        string ui = Path.Combine(layout.Repository, "UIMovies");
        AddPrefabs(ui, ui, Path.Combine(layout.Module, "GUI", "Prefabs"), inputs);
        if (profile.Deployment.SubModuleXml != null)
        {
            ValidateXml(profile.Deployment.SubModuleXml);
            inputs.Add(new(profile.Deployment.SubModuleXml, Path.Combine(layout.Module, "SubModule.xml")));
        }
        if (inputs.Count > 2048) throw new InvalidOperationException("Deployment exceeds the 2048-file bound.");
        foreach (var file in inputs)
        {
            paths.NoLinks(file.Source);
            paths.NoLinks(file.Target);
            if (!paths.Within(file.Target, layout.Module) || Directory.Exists(file.Target)) throw new ArgumentException("Invalid deployment target: " + file.Target);
        }
        return inputs;
    }

    private void AddPrefabs(string directory, string root, string destination, List<DeploymentInput> files)
    {
        paths.NoLinks(directory);
        foreach (string file in Directory.EnumerateFiles(directory, "*.xml"))
        {
            if (files.Count >= 2048) throw new InvalidOperationException("Deployment exceeds the 2048-file bound.");
            files.Add(new(file, Path.Combine(destination, Path.GetRelativePath(root, file))));
        }
        foreach (string child in Directory.EnumerateDirectories(directory)) AddPrefabs(child, root, destination, files);
    }

    private void ValidateXml(string path)
    {
        paths.NoLinks(path);
        var xml = XDocument.Load(path);
        if (xml.ToString().Contains("${", StringComparison.Ordinal) || xml.Root?.Name != "Module" || (string)xml.Root.Element("Id")?.Attribute("value") != "Coop" ||
            !xml.Descendants("DLLName").Any(e => (string)e.Attribute("value") == "Coop.dll"))
            throw new ArgumentException("SubModule.xml must declare module Id Coop and Coop.dll; tokens are not expanded.");
    }
}
