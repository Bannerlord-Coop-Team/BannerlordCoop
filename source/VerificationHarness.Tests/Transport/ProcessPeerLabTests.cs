using System.Diagnostics;
using System.Text.Json;
using VerificationHarness.Planning;
using VerificationHarness.Transport;

namespace VerificationHarness.Tests.Transport;

public sealed class ProcessPeerLabTests
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Tree = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task ConvergeUsesThreeProcessesAndProducesConvergedEvidence()
    {
        ProcessPeerRun run = await ProcessPeerRun.Start("converge");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("process-peer.evidence.v1", run.Evidence.GetProperty("schemaVersion").GetString());
        Assert.Equal("process-peer", run.Evidence.GetProperty("profile").GetString());
        Assert.Equal("functional", run.Evidence.GetProperty("evidenceProfile").GetString());
        Assert.Equal("passed", run.Evidence.GetProperty("verdict").GetString());
        Assert.Equal(Head, run.Evidence.GetProperty("head").GetString());
        Assert.Equal(Tree, run.Evidence.GetProperty("tree").GetString());
        Assert.All(run.Evidence.GetProperty("nodes").EnumerateArray(), node =>
        {
            Assert.Equal("0x00000000000006c1", node.GetProperty("seed").GetString());
            Assert.True(node.GetProperty("deliveryDomainObserved").GetBoolean());
            Assert.True(node.GetProperty("deliveryDomainValid").GetBoolean());
            Assert.Equal(64, node.GetProperty("runtimeArtifactSetDigest").GetString()!.Length);
            Assert.Equal(64, node.GetProperty("runtimeIdentity")
                .GetProperty("sharedRuntimeDigest").GetString()!.Length);
            Assert.Equal(64, node.GetProperty("runtimeIdentity")
                .GetProperty("identityDigest").GetString()!.Length);
        });
        Assert.Equal(
            run.Evidence.GetProperty("manifestRuntimeIdentity")
                .GetProperty("sharedRuntimeDigest").GetString(),
            run.Evidence.GetProperty("controllerRuntimeIdentity")
                .GetProperty("sharedRuntimeDigest").GetString());

        JsonElement topology = run.Evidence.GetProperty("topology");
        Assert.Equal("LiteNetLib", topology.GetProperty("transport").GetString());
        Assert.Equal("1.3.1", topology.GetProperty("transportVersion").GetString());
        Assert.Equal("127.0.0.1", topology.GetProperty("address").GetString());
        Assert.Equal(1, topology.GetProperty("serverCount").GetInt32());
        Assert.Equal(2, topology.GetProperty("clientCount").GetInt32());
        Assert.Equal("Common.Serialization.ProtoBufSerializer", topology.GetProperty("serializer").GetString());

        JsonElement[] processes = run.Evidence.GetProperty("processes").EnumerateArray().ToArray();
        Assert.Equal(3, processes.Length);
        Assert.Equal(3, processes.Select(x => x.GetProperty("processId").GetInt32()).Distinct().Count());
        Assert.All(processes, process =>
        {
            Assert.Equal(0, process.GetProperty("exitCode").GetInt32());
            Assert.False(process.GetProperty("killed").GetBoolean());
        });

        JsonElement digest = run.Evidence.GetProperty("digest");
        Assert.True(digest.GetProperty("converged").GetBoolean());
        string[] digests = digest.GetProperty("byInstance")
            .EnumerateObject()
            .Select(x => x.Value.GetString()!)
            .ToArray();
        Assert.Equal(3, digests.Length);
        Assert.Single(digests.Distinct(StringComparer.Ordinal));
        Assert.All(digests, value => Assert.Equal(64, value.Length));

        Assert.NotEmpty(run.Evidence.GetProperty("wireHashes").EnumerateArray());
        Assert.Equal(64, run.Evidence.GetProperty("artifactHashes").GetProperty("wire-set").GetString()!.Length);
        Assert.True(run.Evidence.GetProperty("completedAtUtc").GetDateTime() >=
                    run.Evidence.GetProperty("startedAtUtc").GetDateTime());
        await AssertProcessesGone(processes);
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task PlannerSeedRunsUnchangedThroughProcessPeerCli()
    {
        var source = new VerificationSourceIdentity(Head, Tree);
        VerificationPlan plan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/Common/Network/MessagePacket.cs" });
        VerificationProfile profile = plan.Profiles.Single(item => item.Id == "process-peer");

        Assert.Contains("{seed}", profile.Arguments);
        Assert.Equal("{artifact.manifest}", profile.Arguments.Last());

        ProcessPeerRun run = await ProcessPeerRun.Start("converge", seed: plan.Seed);
        ProcessPeerRun alternate = await ProcessPeerRun.Start(
            "converge",
            seed: "0x0123456789abcdef");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(0, alternate.ExitCode);
        Assert.Equal(plan.Seed, run.Evidence.GetProperty("seed").GetString());
        Assert.All(run.Evidence.GetProperty("nodes").EnumerateArray(), node =>
            Assert.Equal(plan.Seed, node.GetProperty("seed").GetString()));
        Assert.NotEqual(
            run.Evidence.GetProperty("artifactHashes").GetProperty("wire-set").GetString(),
            alternate.Evidence.GetProperty("artifactHashes").GetProperty("wire-set").GetString());
        Assert.NotEqual(
            run.Evidence.GetProperty("digest").GetProperty("byInstance").GetProperty("server").GetString(),
            alternate.Evidence.GetProperty("digest").GetProperty("byInstance").GetProperty("server").GetString());
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task SourceBoundSuiteRunsTheCompleteProcessMatrix()
    {
        string evidencePath = TemporaryJsonPath();
        ProcessPeerRun run;
        JsonElement persistedEvidence;
        try
        {
            run = await ProcessPeerRun.StartSuite("0xbbbbbbbbbbbbbbbb", evidencePath);
            using JsonDocument persistedDocument = JsonDocument.Parse(File.ReadAllText(evidencePath));
            persistedEvidence = persistedDocument.RootElement.Clone();
        }
        finally
        {
            File.Delete(evidencePath);
        }

        Assert.Equal(0, run.ExitCode);
        JsonElement evidence = run.Evidence;
        Assert.Equal("process-peer-suite.evidence.v1", evidence.GetProperty("schemaVersion").GetString());
        Assert.Equal("passed", evidence.GetProperty("verdict").GetString());
        Assert.Equal(Head, evidence.GetProperty("head").GetString());
        Assert.Equal(Tree, evidence.GetProperty("tree").GetString());
        Assert.Equal("0xbbbbbbbbbbbbbbbb", evidence.GetProperty("seed").GetString());
        Assert.Equal(12, evidence.GetProperty("requiredChecks").EnumerateObject().Count());
        Assert.All(evidence.GetProperty("requiredChecks").EnumerateObject(), check =>
            Assert.True(check.Value.GetBoolean(), check.Name));
        Assert.Equal(7, evidence.GetProperty("scenarios").GetArrayLength());
        Assert.Equal(21, evidence.GetProperty("totalChildProcessCount").GetInt32());
        Assert.Equal(64, evidence.GetProperty("stateDigest").GetString()!.Length);
        Assert.Equal(
            evidence.GetProperty("stateDigest").GetString(),
            persistedEvidence.GetProperty("stateDigest").GetString());
        Assert.True(evidence.GetProperty("requiredChecks").GetProperty("evidence-output-persisted").GetBoolean());
        Assert.Equal(64, evidence.GetProperty("artifactManifestSha256").GetString()!.Length);
        Assert.Equal(64, evidence.GetProperty("replayIdentity").GetString()!.Length);
        Assert.Equal(
            "1.3.1",
            evidence.GetProperty("topology").GetProperty("transportVersion").GetString());
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task AppHostControllerUsesTheSameHostForEveryChild()
    {
        ProcessPeerRun run = await ProcessPeerRun.StartThroughAppHost("converge");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Evidence.GetProperty("requiredChecks")
            .GetProperty("runtime-environment-manifest-match")
            .GetBoolean());
        Assert.True(run.Evidence.GetProperty("requiredChecks")
            .GetProperty("child-runtime-environment-match")
            .GetBoolean());
        string manifestIdentity = run.Evidence
            .GetProperty("manifestRuntimeIdentity")
            .GetProperty("identityDigest")
            .GetString()!;
        Assert.All(run.Evidence.GetProperty("nodes").EnumerateArray(), node =>
            Assert.Equal(
                manifestIdentity,
                node.GetProperty("runtimeIdentity").GetProperty("identityDigest").GetString()));
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task IntentionalDivergenceFailsDigestGateWithoutCrashingNodes()
    {
        ProcessPeerRun run = await ProcessPeerRun.Start("diverge");

        Assert.Equal(5, run.ExitCode);
        Assert.Equal("failed", run.Evidence.GetProperty("verdict").GetString());
        Assert.False(run.Evidence.GetProperty("requiredChecks").GetProperty("digest-convergence").GetBoolean());
        Assert.False(run.Evidence.GetProperty("digest").GetProperty("converged").GetBoolean());
        Assert.All(run.Evidence.GetProperty("processes").EnumerateArray(), process =>
            Assert.Equal(0, process.GetProperty("exitCode").GetInt32()));
        Assert.Contains(
            run.Evidence.GetProperty("failures").EnumerateArray(),
            failure => failure.GetString()!.Contains("digest-convergence", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task MismatchedArtifactManifestFailsBeforeSpawningPeers()
    {
        string manifestPath = TemporaryJsonPath();
        await ProcessPeerArtifactManifestFile.CreateCurrentAsync(Head, Tree, manifestPath);
        var output = new StringWriter();

        int exitCode;
        try
        {
            exitCode = await new ProcessPeerController().RunAsync(
                new[]
                {
                    "--head", Head,
                    "--tree", "cccccccccccccccccccccccccccccccccccccccc",
                    "--artifact-manifest", manifestPath
                },
                output,
                CancellationToken.None);
        }
        finally
        {
            File.Delete(manifestPath);
        }

        Assert.Equal(ProcessPeerController.VerificationFailureExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement evidence = document.RootElement;
        Assert.Equal("failed", evidence.GetProperty("verdict").GetString());
        Assert.False(evidence.GetProperty("requiredChecks")
            .GetProperty("runtime-artifact-manifest-match")
            .GetBoolean());
        Assert.Empty(evidence.GetProperty("processes").EnumerateArray());
    }

    [Fact]
    public async Task NullArtifactMapFailsClosedAndPersistsStructuredEvidence()
    {
        string manifestPath = TemporaryJsonPath();
        string evidencePath = TemporaryJsonPath();
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = "process-peer-artifacts.v2",
                head = Head,
                tree = Tree,
                artifacts = (object?)null,
                artifactSetDigest = new string('a', 64),
                manifestDigest = new string('b', 64),
            }));
        var output = new StringWriter();

        int exitCode;
        try
        {
            exitCode = await new ProcessPeerController().RunAsync(
                new[]
                {
                    "--head", Head,
                    "--tree", Tree,
                    "--artifact-manifest", manifestPath,
                    "--output", evidencePath,
                },
                output,
                CancellationToken.None);

            Assert.Equal(ProcessPeerController.VerificationFailureExitCode, exitCode);
            using JsonDocument stdoutDocument = JsonDocument.Parse(output.ToString());
            using JsonDocument persistedDocument = JsonDocument.Parse(File.ReadAllText(evidencePath));
            JsonElement evidence = stdoutDocument.RootElement;
            Assert.Equal("failed", evidence.GetProperty("verdict").GetString());
            Assert.False(evidence.GetProperty("requiredChecks")
                .GetProperty("runtime-artifact-manifest-match")
                .GetBoolean());
            Assert.True(evidence.GetProperty("requiredChecks")
                .GetProperty("evidence-output-persisted")
                .GetBoolean());
            Assert.Empty(evidence.GetProperty("processes").EnumerateArray());
            Assert.Equal(
                evidence.GetProperty("failures").EnumerateArray().Single().GetString(),
                persistedDocument.RootElement.GetProperty("failures").EnumerateArray().Single().GetString());
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(evidencePath);
        }
    }

    [Fact]
    public async Task RuntimeManifestIncludesSerializerAndAllRuntimeAssemblies()
    {
        string manifestPath = TemporaryJsonPath();
        try
        {
            ProcessPeerArtifactManifest manifest =
                await ProcessPeerArtifactManifestFile.CreateCurrentAsync(Head, Tree, manifestPath);

            Assert.Equal("process-peer-artifacts.v2", manifest.SchemaVersion);
            Assert.Equal(64, manifest.ArtifactSetDigest.Length);
            Assert.True(manifest.RuntimeIdentity.HasValidShape());
            Assert.Equal(64, manifest.RuntimeIdentity.SharedRuntimeDigest.Length);
            Assert.Equal(64, manifest.RuntimeIdentity.IdentityDigest.Length);
            Assert.Contains(
                manifest.Artifacts.Keys,
                key => key.EndsWith("protobuf-net.dll", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                manifest.Artifacts.Keys,
                key => key.EndsWith("Common.dll", StringComparison.OrdinalIgnoreCase));
            Assert.All(manifest.Artifacts.Values, hash => Assert.Equal(64, hash.Length));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [Fact]
    public async Task DifferentFullRuntimeIdentityFailsBeforeSpawningPeers()
    {
        string manifestPath = TemporaryJsonPath();
        try
        {
            ProcessPeerArtifactManifest manifest =
                await ProcessPeerArtifactManifestFile.CreateCurrentAsync(Head, Tree, manifestPath);
            manifest.RuntimeIdentity.HostFileName += ".different";
            manifest.RuntimeIdentity.HostSha256 = new string('c', 64);
            manifest.RuntimeIdentity.RefreshDigests();
            ProcessPeerArtifactManifestFile.RefreshDigests(manifest);
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, TransportJson.Options));

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                ProcessPeerArtifactManifestFile.LoadAndVerify(manifestPath, Head, Tree));

            Assert.Contains("runtime identity", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task ClientReconnectsInSameProcessWithNextGeneration()
    {
        ProcessPeerRun run = await ProcessPeerRun.Start("reconnect");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("passed", run.Evidence.GetProperty("verdict").GetString());
        Assert.True(run.Evidence.GetProperty("requiredChecks").GetProperty("clean-reconnect-generation").GetBoolean());
        JsonElement server = Node(run.Evidence, "server");
        JsonElement clientB = Node(run.Evidence, "client-b");
        Assert.True(server.GetProperty("cleanReconnectObserved").GetBoolean());
        Assert.Equal(2, server.GetProperty("highestGeneration").GetInt32());
        Assert.Equal(2, clientB.GetProperty("highestGeneration").GetInt32());
        Assert.Contains(
            clientB.GetProperty("wireFrames").EnumerateArray(),
            frame => frame.GetProperty("kind").GetString() == "Hello" &&
                     frame.GetProperty("generation").GetInt32() == 2 &&
                     frame.GetProperty("sequence").GetInt64() == 1);
    }

    [Theory]
    [InlineData("malformed", "malformed-frame")]
    [InlineData("out-of-sequence", "invalid-sequence")]
    [InlineData("corrupt-acknowledgement", "digest-mismatch")]
    [Trait("Category", "ProcessPeer")]
    public async Task InvalidFramesAreRejectedWithExactEvidence(string scenario, string rejectionCode)
    {
        ProcessPeerRun run = await ProcessPeerRun.Start(scenario);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("passed", run.Evidence.GetProperty("verdict").GetString());
        Assert.True(run.Evidence.GetProperty("requiredChecks").GetProperty(rejectionCode + "-rejected").GetBoolean());
        Assert.Equal(rejectionCode, Node(run.Evidence, "server").GetProperty("rejectionCode").GetString());
        Assert.Equal(rejectionCode, Node(run.Evidence, "client-b").GetProperty("rejectionCode").GetString());
        if (scenario == "corrupt-acknowledgement")
        {
            Assert.Contains(
                Node(run.Evidence, "client-b").GetProperty("wireFrames").EnumerateArray(),
                frame => frame.GetProperty("direction").GetString() == "sent" &&
                         frame.GetProperty("kind").GetString() == "Acknowledgement");
            Assert.Contains(
                Node(run.Evidence, "server").GetProperty("wireFrames").EnumerateArray(),
                frame => frame.GetProperty("direction").GetString() == "sent" &&
                         frame.GetProperty("kind").GetString() == "Rejection");
        }
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task DeadlineKillsEveryChildAndLeavesNoOrphans()
    {
        var stopwatch = Stopwatch.StartNew();
        ProcessPeerRun run = await ProcessPeerRun.Start("timeout", timeoutMilliseconds: 500);
        stopwatch.Stop();

        Assert.Equal(6, run.ExitCode);
        Assert.Equal("failed", run.Evidence.GetProperty("verdict").GetString());
        Assert.False(run.Evidence.GetProperty("requiredChecks").GetProperty("completed-before-deadline").GetBoolean());
        JsonElement[] processes = run.Evidence.GetProperty("processes").EnumerateArray().ToArray();
        Assert.Equal(3, processes.Length);
        Assert.All(processes, process => Assert.True(process.GetProperty("killed").GetBoolean()));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        await AssertProcessesGone(processes);
    }

    [Fact]
    [Trait("Category", "ProcessPeer")]
    public async Task UnknownScenarioFailsClosedBeforeSpawningChildren()
    {
        ProcessPeerRun run = await ProcessPeerRun.Start("unknown", expectEvidence: false);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("Unknown process-peer scenario", run.StandardError, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Undefined, run.Evidence.ValueKind);
    }

    [Fact]
    public void SuiteRejectsMissingOtherwiseTrueRequiredCheck()
    {
        Dictionary<string, bool> requiredChecks = ProcessPeerSuiteController
            .ExpectedRequiredCheckIds(TransportScenarios.Converge)
            .ToDictionary(check => check, _ => true, StringComparer.Ordinal);

        Assert.True(ProcessPeerSuiteController.HasExpectedRequiredCheckCatalog(
            TransportScenarios.Converge,
            requiredChecks));

        Assert.True(requiredChecks.Remove("loaded-transport-version"));
        Assert.False(ProcessPeerSuiteController.HasExpectedRequiredCheckCatalog(
            TransportScenarios.Converge,
            requiredChecks));
    }

    [Fact]
    public void SuiteRejectsEvidenceFromTheWrongScenario()
    {
        ProcessPeerOptions options = ProcessPeerOptions.Parse(new[]
        {
            "--head", Head,
            "--tree", Tree,
            "--seed", "0xbbbbbbbbbbbbbbbb",
            "--artifact-manifest", "manifest.json"
        });
        var evidence = new ProcessPeerEvidence
        {
            Head = Head,
            Tree = Tree,
            Seed = options.Seed,
            Scenario = TransportScenarios.Malformed,
            ArtifactManifestSha256 = new string('a', 64)
        };
        evidence.RequiredChecks["runtime-artifact-manifest-match"] = true;

        Assert.True(ProcessPeerSuiteController.SourceMatches(
            options,
            TransportScenarios.Malformed,
            evidence));
        Assert.False(ProcessPeerSuiteController.SourceMatches(
            options,
            TransportScenarios.OutOfSequence,
            evidence));
    }

    private static JsonElement Node(JsonElement evidence, string instanceId)
    {
        return evidence.GetProperty("nodes")
            .EnumerateArray()
            .Single(x => x.GetProperty("instanceId").GetString() == instanceId);
    }

    private static async Task AssertProcessesGone(IEnumerable<JsonElement> processes)
    {
        foreach (JsonElement processEvidence in processes)
        {
            int processId = processEvidence.GetProperty("processId").GetInt32();
            bool gone = false;
            for (int attempt = 0; attempt < 20 && !gone; attempt++)
            {
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    gone = process.HasExited;
                }
                catch (ArgumentException)
                {
                    gone = true;
                }

                if (!gone) await Task.Delay(25);
            }

            Assert.True(gone, $"Transport child process {processId} is still running.");
        }
    }

    private sealed class ProcessPeerRun
    {
        public int ExitCode { get; private init; }
        public JsonElement Evidence { get; private init; }
        public string StandardError { get; private init; } = string.Empty;

        public static async Task<ProcessPeerRun> Start(
            string scenario,
            int timeoutMilliseconds = 6000,
            bool expectEvidence = true,
            string? seed = null)
        {
            return await StartCommand(
                "process-peer",
                startInfo =>
                {
                    AddOption(startInfo, "--scenario", scenario);
                    AddOption(startInfo, "--timeout-ms", timeoutMilliseconds.ToString());
                    if (seed != null) AddOption(startInfo, "--seed", seed);
                },
                expectEvidence,
                TimeSpan.FromSeconds(20));
        }

        public static async Task<ProcessPeerRun> StartSuite(string seed, string evidencePath)
        {
            return await StartCommand(
                "process-peer-suite",
                startInfo =>
                {
                    AddOption(startInfo, "--timeout-ms", "6000");
                    AddOption(startInfo, "--seed", seed);
                    AddOption(startInfo, "--output", evidencePath);
                },
                true,
                TimeSpan.FromSeconds(30));
        }

        public static async Task<ProcessPeerRun> StartThroughAppHost(string scenario)
        {
            return await StartCommand(
                "process-peer",
                startInfo =>
                {
                    AddOption(startInfo, "--scenario", scenario);
                    AddOption(startInfo, "--timeout-ms", "6000");
                },
                true,
                TimeSpan.FromSeconds(20),
                useAppHost: true);
        }

        private static async Task<ProcessPeerRun> StartCommand(
            string command,
            Action<ProcessStartInfo> configure,
            bool expectEvidence,
            TimeSpan timeout,
            bool useAppHost = false)
        {
            string hostAssembly = typeof(VerificationHarness.Program).Assembly.Location;
            string runtimeConfig = Path.ChangeExtension(hostAssembly, ".runtimeconfig.json");
            string dependencyManifest = Path.ChangeExtension(hostAssembly, ".deps.json");
            string dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            string manifestPath = TemporaryJsonPath();
            await CreateManifestAsync(
                dotnetHost,
                hostAssembly,
                runtimeConfig,
                dependencyManifest,
                manifestPath,
                useAppHost);
            ProcessStartInfo startInfo = CreateStartInfo(
                dotnetHost,
                hostAssembly,
                runtimeConfig,
                dependencyManifest,
                command,
                useAppHost);
            AddOption(startInfo, "--head", Head);
            AddOption(startInfo, "--tree", Tree);
            AddOption(startInfo, "--artifact-manifest", manifestPath);
            configure(startInfo);

            using var process = new Process { StartInfo = startInfo };
            try
            {
                Assert.True(process.Start());
                Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                Task<string> standardError = process.StandardError.ReadToEndAsync();
                using var cancellation = new CancellationTokenSource(timeout);
                try
                {
                    await process.WaitForExitAsync(cancellation.Token);
                }
                catch
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    throw;
                }

                string output = await standardOutput;
                string error = await standardError;
                JsonElement evidence = default;
                if (expectEvidence)
                {
                    string line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Single();
                    using JsonDocument document = JsonDocument.Parse(line);
                    evidence = document.RootElement.Clone();
                }

                return new ProcessPeerRun
                {
                    ExitCode = process.ExitCode,
                    Evidence = evidence,
                    StandardError = error
                };
            }
            finally
            {
                File.Delete(manifestPath);
            }
        }

        private static async Task CreateManifestAsync(
            string dotnetHost,
            string hostAssembly,
            string runtimeConfig,
            string dependencyManifest,
            string manifestPath,
            bool useAppHost)
        {
            ProcessStartInfo startInfo = CreateStartInfo(
                dotnetHost,
                hostAssembly,
                runtimeConfig,
                dependencyManifest,
                "process-peer-manifest",
                useAppHost);
            AddOption(startInfo, "--head", Head);
            AddOption(startInfo, "--tree", Tree);
            AddOption(startInfo, "--output", manifestPath);
            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start());
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cancellation.Token);
            string output = await standardOutput;
            string error = await standardError;
            Assert.True(process.ExitCode == 0, $"Manifest command failed: {error}");
            Assert.Equal(64, output.Trim().Length);
        }

        private static ProcessStartInfo CreateStartInfo(
            string dotnetHost,
            string hostAssembly,
            string runtimeConfig,
            string dependencyManifest,
            string command,
            bool useAppHost)
        {
            string executable = useAppHost
                ? Path.Combine(
                    Path.GetDirectoryName(hostAssembly)!,
                    Path.GetFileNameWithoutExtension(hostAssembly) +
                    (OperatingSystem.IsWindows() ? ".exe" : string.Empty))
                : dotnetHost;
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (!useAppHost)
            {
                startInfo.ArgumentList.Add("exec");
                startInfo.ArgumentList.Add("--runtimeconfig");
                startInfo.ArgumentList.Add(runtimeConfig);
                startInfo.ArgumentList.Add("--depsfile");
                startInfo.ArgumentList.Add(dependencyManifest);
                startInfo.ArgumentList.Add(hostAssembly);
            }
            startInfo.ArgumentList.Add(command);
            return startInfo;
        }

        private static void AddOption(ProcessStartInfo startInfo, string name, string value)
        {
            startInfo.ArgumentList.Add(name);
            startInfo.ArgumentList.Add(value);
        }
    }

    private static string TemporaryJsonPath() =>
        Path.Combine(Path.GetTempPath(), $"process-peer-{Guid.NewGuid():N}.json");
}
