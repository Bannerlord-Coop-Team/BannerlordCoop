using System.Diagnostics;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class ModDeploymentServiceTests : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "DeployFixture-" + Guid.NewGuid().ToString("N"));
    private readonly DeploymentPaths paths = new();
    private readonly CoopMcpServerSettings settings = new();
    private readonly EnvironmentFixture environment = new();
    private readonly BuildFixture build = new();
    private readonly FileFixture files = new();
    private readonly RunOrchestrator runs;
    private readonly BuildCleanupRecovery cleanup = new();
    private readonly string solution;
    private readonly string module;
    private readonly string durable;
    private readonly string repository;
    private const string Xml = "<Module><Id value=\"Coop\"/><SubModules><SubModule><DLLName value=\"Coop.dll\"/></SubModule></SubModules></Module>";

    public ModDeploymentServiceTests()
    {
        repository = Path.Combine(root, "repository");
        module = Path.Combine(root, "game", "Modules", "Coop");
        durable = Path.Combine(root, "durable");
        solution = Path.Combine(repository, "source", "Coop.sln");
        Write(Path.Combine(repository, ".git"), "fixture");
        Write(Path.Combine(repository, "Deploy.targets"), "<Project/>");
        Write(Path.Combine(repository, "deploy", "SubModule.xml"), Xml);
        Write(Path.Combine(repository, "UIMovies", "Fixture.xml"), "<Prefab/>");
        Write(solution, "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        foreach (string project in new[] { "Coop", "Common", "Coop.Core", "GameInterface", "Missions", "Coop.Steam", "Coop.CrashReporter" })
        {
            Write(Path.Combine(repository, "source", project, project + ".csproj"), "<Project/>");
            File.AppendAllText(solution, $"Project = \"{project}\", \"{project}\\{project}.csproj\"\n");
        }
        Write(Path.Combine(root, "game", "Modules", "Native", "SubModule.xml"), "<Module/>");
        Write(Path.Combine(module, "SubModule.xml"), Xml);
        string exe = Path.Combine(root, "game", "bin", "Win64_Shipping_Client", "Bannerlord.exe");
        Write(exe, "fake never launched");
        string msbuild = Path.Combine(root, "toolchain", "MSBuild.exe");
        Write(msbuild, "fake never executed");
        settings.Profiles["fixture"] = new LaunchProfile { Executable = exe,
            Deployment = new DeploymentSettings { DurableRoot = durable, MsbuildPath = msbuild } };
        runs = new RunOrchestrator(settings, null, null, null, null, null, new DeploymentLease(paths), cleanup);
        build.Action = CreateOutputs;
    }

    private ModDeploymentService Service() => new(settings, runs, new DeploymentLease(paths), environment,
        new DeploymentPlan(paths, files), build, files, new BridgeBuildInspector(), paths);

    [Fact]
    public async Task DelayedLogFinalizationRetainsLeaseAndReusesDisposalTasksUntilRecovery()
    {
        if (!OperatingSystem.IsWindows()) return;
        var releaseFinalization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new List<DelayedFinalizationFileStream>();
        var runner = new WindowsJobBuildProcessRunner(cleanup, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(250), path =>
        {
            var log = new DelayedFinalizationFileStream(path, releaseFinalization.Task);
            logs.Add(log);
            return log;
        });
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root,
        };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-Command",
            "[Console]::Out.Write('buffered stdout'); [Console]::Error.Write('buffered stderr')" }) info.ArgumentList.Add(arg);
        build.RunBuild = (artifacts, token) => runner.RunAsync(info, artifacts, "Coop", token);
        try
        {
            var report = await Service().DeployAsync(solution, "fixture", default).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("build_cleanup_required", report.State);
            Assert.Empty(report.Files);
            Assert.Equal(2, logs.Count);
            await Task.WhenAll(logs.Select(log => log.FinalizationStarted.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<IOException>(() => new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]));
            await Assert.ThrowsAsync<BuildCleanupException>(() => Service().DeployAsync(solution, "fixture", default).WaitAsync(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<BuildCleanupException>(() => runs.StartAsync("fixture", 0, default).WaitAsync(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<BuildCleanupException>(() => runs.StartClientAsync("absent", 1, default).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, build.Calls);
            foreach (var log in logs)
            {
                Assert.Equal(1, log.DisposalCalls);
                Assert.False(log.FinalizationFinished.Task.IsCompleted);
                Assert.Throws<IOException>(() => File.Open(log.Name, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            }
            releaseFinalization.SetResult();
            await Task.WhenAll(logs.Select(log => log.FinalizationFinished.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            build.RunBuild = null;
            Assert.Equal("deployed", (await Service().DeployAsync(solution, "fixture", default)).State);
            using var available = new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]);
            foreach (var log in logs)
            {
                Assert.Equal(1, log.DisposalCalls);
                using (File.Open(log.Name, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                Assert.Equal(log.Name.EndsWith("stdout.log") ? "buffered stdout" : "buffered stderr", File.ReadAllText(log.Name));
            }
        }
        finally
        {
            releaseFinalization.TrySetResult();
            await Task.WhenAll(logs.Select(log => log.FinalizationFinished.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            await cleanup.RecoverAsync();
        }
    }

    [Fact]
    public async Task UnconfirmedBuildCleanupRetainsLeaseAndBlocksStartsUntilRecovery()
    {
        bool treeAlive = true;
        int cleanupAttempts = 0;
        build.Action = _ =>
        {
            cleanup.Retain(() =>
            {
                cleanupAttempts++;
                if (treeAlive) throw new IOException("injected unconfirmed tree termination");
                return Task.CompletedTask;
            });
            throw new BuildCleanupException(cleanup, new IOException("injected cleanup deadline"));
        };
        try
        {
            var report = await Service().DeployAsync(solution, "fixture", default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("build_cleanup_required", report.State);
            Assert.Empty(report.Files);
            Assert.Throws<IOException>(() => new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]));
            await Assert.ThrowsAsync<BuildCleanupException>(() => Service().DeployAsync(solution, "fixture", default));
            await Assert.ThrowsAsync<BuildCleanupException>(() => runs.StartAsync("fixture", 0, default));
            await Assert.ThrowsAsync<BuildCleanupException>(() => runs.StartClientAsync("absent", 1, default));
            Assert.Equal(1, build.Calls);
            Assert.Equal(3, cleanupAttempts);
            treeAlive = false;
            build.Action = CreateOutputs;
            Assert.Equal("deployed", (await Service().DeployAsync(solution, "fixture", default)).State);
            using var available = new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]);
            Assert.Equal(4, cleanupAttempts);
        }
        finally
        {
            treeAlive = false;
            await cleanup.RecoverAsync();
        }
    }

    [Fact]
    public async Task DeploysBoundedOutputsWithFreshVerifiedBackupsAndLeavesConfigurationsUntouched()
    {
        string target = Path.Combine(module, "bin", "Win64_Shipping_Client", "Coop.dll");
        CopyAssembly(typeof(ModDeploymentService).Assembly.Location, target);
        File.SetAttributes(target, FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.Archive);
        FileAttributes attributes = File.GetAttributes(target);
        string optin = Path.Combine(module, "naval.optin");
        Write(optin, "operator-owned bytes\r\n");
        string engine = Path.Combine(module, "DedicatedServer", "engine.dll");
        Write(engine, "not a deployment target");
        var original = files.Inspect(target);
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("deployed", report.State);
        Assert.Equal("Release", report.Configuration);
        Assert.Equal("Release", build.Configuration);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(report.ArtifactDirectory, "manifest.json")));
        Assert.Equal("Release", manifest.RootElement.GetProperty("configuration").GetString());
        Assert.True(File.Exists(Path.Combine(report.ArtifactDirectory, "manifest.json")));
        var coop = Assert.Single(report.Files, e => e.Target == target);
        Assert.Equal(original, files.Inspect(coop.Backup));
        Assert.Equal(coop.Replacement, files.Inspect(target));
        Assert.NotEmpty(coop.Replacement.Mvid);
        Assert.Equal(attributes, File.GetAttributes(target));
        Assert.Equal(Xml, File.ReadAllText(Path.Combine(module, "SubModule.xml")));
        Assert.Equal("operator-owned bytes\r\n", File.ReadAllText(optin));
        Assert.DoesNotContain(report.Files, e => e.Target.Contains("DedicatedServer") || e.Target.Contains("TaleWorlds") || e.Target.EndsWith("naval.optin"));
        Assert.Equal("not a deployment target", File.ReadAllText(engine));
        Assert.False(File.Exists(Path.Combine(durable, "recovery-required.json")));
        Assert.Contains(report.Files, e => !e.OriginallyExisted && e.Original == null && e.ApplyAttempted);
        Assert.True(environment.SpaceChecks >= 6);
    }

    [Fact]
    public async Task ApplyFailureRestoresBytesAttributesAndOriginalAbsenceIncludingExplicitXml()
    {
        string replacement = Path.Combine(root, "explicit.xml");
        Write(replacement, Xml.Replace("<Module>", "<Module><!--deliberate replacement-->"));
        settings.Profiles["fixture"].Deployment.SubModuleXml = replacement;
        string originalXml = Path.Combine(module, "SubModule.xml");
        File.SetAttributes(originalXml, FileAttributes.ReadOnly | FileAttributes.Archive);
        var attributes = File.GetAttributes(originalXml);
        files.AfterCopy = (_, target) => { if (target == originalXml) throw new IOException("injected apply failure after writing XML"); };
        var report = await Service().DeployAsync(solution, "fixture", default);
        // Stop injecting during rollback so the backup copy can finish.
        Assert.Equal("rolled_back", report.State);
        Assert.Empty(report.UnresolvedRestoration);
        Assert.Equal(Xml, File.ReadAllText(originalXml));
        Assert.Equal(attributes, File.GetAttributes(originalXml));
        Assert.All(report.Files, e => Assert.Equal("verified", e.Restoration));
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
        Assert.False(Directory.Exists(Path.Combine(module, "GUI")));
    }

    [Fact]
    public async Task FailedRestorationPersistsMarkerAndBlocksStartsAndFurtherDeployments()
    {
        string target = Path.Combine(module, "bin", "Win64_Shipping_Client", "Coop.dll");
        CopyAssembly(typeof(ModDeploymentService).Assembly.Location, target);
        files.FailRestore = true;
        files.AfterCopy = (_, path) => { if (path == target) throw new IOException("apply failed"); };
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("rollback_failed", report.State);
        Assert.Contains(report.UnresolvedRestoration, e => e.Contains(target));
        Assert.True(File.Exists(Path.Combine(durable, "recovery-required.json")));
        Assert.Throws<InvalidOperationException>(() => new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().DeployAsync(solution, "fixture", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runs.StartAsync("fixture", 0, default));
    }

    [Fact]
    public async Task BackupCorruptionFailsBeforeAnyGameMutation()
    {
        string target = Path.Combine(module, "bin", "Win64_Shipping_Client", "Coop.dll");
        CopyAssembly(typeof(ModDeploymentService).Assembly.Location, target);
        var original = files.Inspect(target);
        files.AfterCopy = (_, path) => { if (path.Contains(Path.DirectorySeparatorChar + "backup" + Path.DirectorySeparatorChar)) File.AppendAllText(path, "corruption"); };
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("failed_before_apply", report.State);
        Assert.Equal(original, files.Inspect(target));
        Assert.False(File.Exists(Path.Combine(durable, "recovery-required.json")));
    }

    [Fact]
    public async Task BuildFailureAndInsufficientDiskNeverModifyGameTargets()
    {
        build.Action = _ => throw new InvalidOperationException("fake build failed");
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("failed_before_apply", report.State);
        Assert.Empty(report.Files);
        environment.NoSpace = true;
        await Assert.ThrowsAsync<IOException>(() => Service().DeployAsync(solution, "fixture", default));
        Assert.Equal(Xml, File.ReadAllText(Path.Combine(module, "SubModule.xml")));
    }

    [Fact]
    public async Task ActiveProcessesAndConcurrentDeploymentAreRejected()
    {
        environment.Active = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().DeployAsync(solution, "fixture", default));
        Assert.Equal(0, build.Calls);
        environment.Active = false;
        build.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DeploymentReport> first = Service().DeployAsync(solution, "fixture", default);
        await build.Entered.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().DeployAsync(solution, "fixture", default));
        Task<RunView> start = runs.StartAsync("fixture", 0, new CancellationToken(true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        build.Block.SetResult();
        Assert.Equal("deployed", (await first).State);
    }

    [Fact]
    public async Task CrossSessionLeaseAndCancellationDoNotLeaveAppliedFiles()
    {
        using (new DeploymentLease(paths).Acquire(settings.Profiles["fixture"]))
            await Assert.ThrowsAsync<IOException>(() => Service().DeployAsync(solution, "fixture", default));
        using var cancellation = new CancellationTokenSource();
        files.AfterCopy = (_, target) => { if (target.StartsWith(module + Path.DirectorySeparatorChar)) cancellation.Cancel(); };
        var report = await Service().DeployAsync(solution, "fixture", cancellation.Token);
        Assert.Equal("rolled_back", report.State);
        Assert.Empty(report.UnresolvedRestoration);
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
    }

    [Fact]
    public async Task ProcessAppearingDuringBuildStopsBeforeApply()
    {
        build.Action = path => { CreateOutputs(path); environment.Active = true; };
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("failed_before_apply", report.State);
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
        Assert.False(File.Exists(Path.Combine(durable, "recovery-required.json")));
    }

    [Fact]
    public async Task XmlReplacementPreservesExactConfiguredBytesWithoutTemplating()
    {
        string xml = Path.Combine(root, "explicit.xml");
        Write(xml, "<?xml version=\"1.0\"?>\r\n" + Xml + "\r\n");
        settings.Profiles["fixture"].Deployment.SubModuleXml = xml;
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("deployed", report.State);
        var entry = Assert.Single(report.Files, f => f.Target.EndsWith("SubModule.xml"));
        Assert.Equal(File.ReadAllBytes(xml), File.ReadAllBytes(entry.Target));
        Assert.Equal(Xml, File.ReadAllText(entry.Backup));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MixedProjectCopiesAndUnknownDependenciesFailBeforeApply(bool mixedProject)
    {
        build.Action = path =>
        {
            CreateOutputs(path);
            CopyAssembly(typeof(ModDeploymentService).Assembly.Location, Path.Combine(path, "source", "Coop", "bin", build.Configuration,
                mixedProject ? "Common.dll" : "Unexpected.Dependency.dll"));
        };
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("failed_before_apply", report.State);
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
    }

    [Fact]
    public async Task DifferentBridgeBuildCannotPassDebugDeploymentVerification()
    {
        build.Action = path =>
        {
            CreateOutputs(path);
            CopyAssembly(typeof(ModDeploymentService).Assembly.Location, Path.Combine(path, "source", "Coop", "bin", build.Configuration, "Coop.dll"));
        };
        var report = await Service().DeployAsync(solution, "fixture", default, "Debug");
        Assert.Equal("failed_before_apply", report.State);
        Assert.Contains("compatible DEBUG", report.Error);
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
    }

    [Fact]
    public async Task OptionalNavalOutputIsRequiredAndIncludedWithoutOptinMutation()
    {
        string project = Path.Combine(repository, "source", "Missions.Naval", "Missions.Naval.csproj");
        Write(project, "<Project/>");
        File.AppendAllText(solution, "Project = \"Missions.Naval\", \"Missions.Naval\\Missions.Naval.csproj\"\n");
        var failed = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("failed_before_apply", failed.State);
        build.Action = path => { CreateOutputs(path); CopyAssembly(typeof(ModDeploymentServiceTests).Assembly.Location,
            Path.Combine(path, "source", "Missions.Naval", "bin", build.Configuration, "netstandard2.0", "Missions.Naval.dll")); };
        var report = await Service().DeployAsync(solution, "fixture", default);
        Assert.Equal("deployed", report.State);
        Assert.Contains(report.Files, e => e.Target.EndsWith("Missions.Naval.dll"));
        Assert.Equal(Xml, File.ReadAllText(Path.Combine(module, "SubModule.xml")));
    }

    [Theory]
    [InlineData("Release")]
    [InlineData("Debug")]
    public async Task SelectedConfigurationFlowsThroughBuildOutputsReportAndManifest(string configuration)
    {
        build.Action = path =>
        {
            CreateOutputs(path);
            // Release need not contain bridge metadata; Debug uses the compatible fixture assembly.
            if (configuration == "Release") CopyAssembly(typeof(ModDeploymentService).Assembly.Location,
                Path.Combine(path, "source", "Coop", "bin", configuration, "Coop.dll"));
        };
        var report = await Service().DeployAsync(solution, "fixture", default, configuration);
        Assert.Equal("deployed", report.State);
        Assert.Equal(configuration, build.Configuration);
        Assert.Equal(configuration, report.Configuration);
        Assert.All(report.Files.Where(e => e.Source.EndsWith(".dll") || e.Source.EndsWith(".exe")),
            e => Assert.Contains(Path.DirectorySeparatorChar + configuration + Path.DirectorySeparatorChar, e.Source));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(report.ArtifactDirectory, "manifest.json")));
        Assert.Equal(configuration, manifest.RootElement.GetProperty("configuration").GetString());
        var info = ModBuildService.CreateStartInfo(repository, "MSBuild.exe", durable, "Coop", configuration);
        Assert.Contains("-p:Configuration=" + configuration, info.ArgumentList);
        var naval = ModBuildService.CreateStartInfo(repository, "MSBuild.exe", durable, "Missions.Naval", configuration);
        Assert.Contains("-p:Configuration=" + configuration, naval.ArgumentList);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("debug")]
    [InlineData("release")]
    [InlineData("Retail")]
    [InlineData("Debug;ModName=Coop")]
    [InlineData("../Debug")]
    public async Task InvalidConfigurationIsRejectedBeforeBuildOrArtifacts(string configuration)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().DeployAsync(solution, "fixture", default, configuration));
        Assert.Equal(0, build.Calls);
        Assert.False(Directory.Exists(durable));
        Assert.Throws<ArgumentException>(() => ModBuildService.CreateStartInfo(repository, "MSBuild.exe", durable, "Coop", configuration));
    }

    [Fact]
    public async Task DebugDoesNotFallBackToExistingReleaseOutputs()
    {
        build.Action = path =>
        {
            build.Configuration = "Release";
            CreateOutputs(path);
        };
        var report = await Service().DeployAsync(solution, "fixture", default, "Debug");
        Assert.Equal("failed_before_apply", report.State);
        Assert.Equal("Debug", report.Configuration);
        Assert.Contains("Required Debug build output missing", report.Error);
        Assert.False(Directory.Exists(Path.Combine(module, "bin")));
    }

    [Fact]
    public void RejectsWrongLayoutsDisabledProfilesTempRootsAndTemplateXml()
    {
        var plan = new DeploymentPlan(paths, files);
        var profile = settings.Profiles["fixture"];
        Assert.Throws<ArgumentException>(() => plan.Validate("source/Coop.sln", profile));
        Assert.Throws<ArgumentException>(() => plan.Validate(solution.Replace("Coop.sln", "Other.sln"), profile));
        var deployment = profile.Deployment;
        profile.Deployment = null;
        Assert.Throws<ArgumentException>(() => plan.Validate(solution, profile));
        profile.Deployment = deployment;
        deployment.DurableRoot = Path.Combine(Path.GetTempPath(), "not-durable");
        Assert.Throws<ArgumentException>(() => plan.Validate(solution, profile));
        deployment.DurableRoot = durable;
        deployment.SubModuleXml = Path.Combine(repository, "deploy", "SubModule.xml");
        Write(deployment.SubModuleXml, Xml.Replace("<Module>", "<Module version=\"${version}\">"));
        Assert.Throws<ArgumentException>(() => plan.Validate(solution, profile));
    }

    [Fact]
    public void BuildArgumentsDisableEveryDeployDestinationAndNeverAcceptShellCommands()
    {
        var info = ModBuildService.CreateStartInfo(repository, settings.Profiles["fixture"].Deployment.MsbuildPath, durable, "Coop");
        Assert.False(info.UseShellExecute);
        Assert.Contains("-t:Rebuild", info.ArgumentList);
        Assert.Contains("-p:Configuration=Release", info.ArgumentList);
        Assert.Contains("-p:ModName=", info.ArgumentList);
        Assert.Contains("-p:PostBuildEvent=", info.ArgumentList);
        Assert.Contains("-p:PreBuildEvent=", info.ArgumentList);
        foreach (string property in new[] { "ModsRoot", "ModDir", "ModBinDir", "ModPrefabDir" })
            Assert.StartsWith("-p:" + property + "=" + durable, Assert.Single(info.ArgumentList, a => a.StartsWith("-p:" + property + "=")));
        Assert.Throws<ArgumentException>(() => ModBuildService.CreateStartInfo(repository, "cmd.exe", durable, "anything & launch"));
        var naval = ModBuildService.CreateStartInfo(repository, "MSBuild.exe", durable, "Missions.Naval");
        Assert.Contains("-p:BuildProjectReferences=false", naval.ArgumentList);
        var escaped = ModBuildService.CreateStartInfo(repository, "MSBuild.exe", durable + ";ModName=Coop%", "Coop");
        string redirected = Assert.Single(escaped.ArgumentList, a => a.StartsWith("-p:ModsRoot="));
        Assert.DoesNotContain(";", redirected);
        Assert.Contains("%3BModName=Coop%25", redirected);
    }

    private void CreateOutputs(string repository)
    {
        foreach (string project in new[] { "Coop", "Common", "Coop.Core", "GameInterface", "Missions", "Coop.Steam", "Coop.CrashReporter" })
        {
            string output = Path.Combine(repository, "source", project, "bin", build.Configuration);
            if (project is not "Coop" and not "Coop.CrashReporter") output = Path.Combine(output, "netstandard2.0");
            string name = project + (project == "Coop.CrashReporter" ? ".exe" : ".dll");
            CopyAssembly(typeof(ModDeploymentServiceTests).Assembly.Location, Path.Combine(output, name));
            if (project != "Coop") CopyAssembly(typeof(ModDeploymentServiceTests).Assembly.Location, Path.Combine(repository, "source", "Coop", "bin", build.Configuration, name));
        }
        Write(Path.Combine(repository, "source", "Coop", "bin", build.Configuration, "TaleWorlds.Library.dll"), "must not ship");
        Write(Path.Combine(repository, "source", "Coop", "bin", build.Configuration, "publicized", "Autofac.dll"), "must not recurse");
    }

    private static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text); }
    private static void CopyAssembly(string source, string target) { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, true); }

    private sealed class DelayedFinalizationFileStream : FileStream
    {
        private readonly Task release;
        private int disposalCalls;
        public int DisposalCalls => Volatile.Read(ref disposalCalls);
        public TaskCompletionSource FinalizationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinalizationFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DelayedFinalizationFileStream(string path, Task release)
            : base(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true)
        {
            this.release = release;
        }

        public override async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposalCalls);
            FinalizationStarted.TrySetResult();
            await release;
            await base.DisposeAsync();
            FinalizationFinished.TrySetResult();
        }
    }

    private sealed class BuildFixture : IModBuildService
    {
        public Action<string> Action;
        public Func<string, CancellationToken, Task> RunBuild;
        public int Calls;
        public string Configuration;
        public TaskCompletionSource Block;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BuildAsync(string repository, DeploymentSettings settings, string artifacts, CancellationToken cancellationToken, string configuration = "Release")
        {
            Configuration = configuration;
            Calls++; Entered.TrySetResult();
            if (Block != null) await Block.Task.WaitAsync(cancellationToken);
            if (RunBuild != null) await RunBuild(artifacts, cancellationToken);
            Action(repository);
        }
    }
    private sealed class EnvironmentFixture : IDeploymentEnvironment
    {
        public bool Active, NoSpace;
        public int SpaceChecks;
        public void RequireIdle() { if (Active) throw new InvalidOperationException("Bannerlord (PID 123) still active"); }
        public void RequireSpace(string path, long bytes) { SpaceChecks++; if (NoSpace) throw new IOException("insufficient disk"); }
    }
    private sealed class FileFixture : IDeploymentFiles
    {
        private readonly DeploymentFiles actual = new();
        public Action<string, string> AfterCopy;
        public bool FailRestore;
        public FileEvidence Inspect(string path) => actual.Inspect(path);
        public void Copy(string source, string target)
        {
            if (FailRestore && source.Contains(Path.DirectorySeparatorChar + "backup" + Path.DirectorySeparatorChar)) throw new IOException("restore blocked");
            actual.Copy(source, target);
            var callback = AfterCopy;
            try { callback?.Invoke(source, target); }
            catch { AfterCopy = null; throw; }
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, true);
    }
}
