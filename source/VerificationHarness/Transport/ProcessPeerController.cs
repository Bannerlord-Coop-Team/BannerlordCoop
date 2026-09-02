using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Common.Serialization;
using LiteNetLib;
using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public sealed class ProcessPeerController
{
    public const int VerificationFailureExitCode = 5;
    public const int TimeoutExitCode = 6;

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (output == null) throw new ArgumentNullException(nameof(output));

        ProcessPeerOptions options = ProcessPeerOptions.Parse(args);
        ProcessPeerArtifactManifest artifactManifest;
        try
        {
            artifactManifest = ProcessPeerArtifactManifestFile.LoadAndVerify(
                options.ArtifactManifestPath,
                options.Head,
                options.Tree);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return await WriteArtifactManifestFailureAsync(
                options,
                output,
                $"{ex.GetType().Name}:{ex.Message}");
        }

        DateTime startedAtUtc = DateTime.UtcNow;
        int port = 0;
        var children = new List<TransportChildProcess>();
        var cleanupFailures = new List<string>();
        bool orphanCleanupComplete = true;
        bool controllerTimedOut = false;
        string? controllerFailure = null;

        try
        {
            int childTimeout = OptionsChildTimeout(options);
            TransportChildProcess server = StartChild(
                "server", "server", 0, options.Scenario, childTimeout, options.Seed);
            children.Add(server);

            string? readyLine = await ReadLineBefore(
                server.Process.StandardOutput,
                TimeSpan.FromMilliseconds(Math.Min(3000, options.TimeoutMilliseconds)),
                cancellationToken);
            if (!TryParseReady(readyLine, server.Process.Id, out port))
            {
                controllerFailure = "The transport server did not emit a valid ready event.";
                server.BeginCaptureOutput();
            }
            else
            {
                server.BeginCaptureOutput();
                TransportChildProcess clientA = StartChild(
                    "client", "client-a", port, options.Scenario, childTimeout, options.Seed);
                clientA.BeginCaptureOutput();
                children.Add(clientA);

                TransportChildProcess clientB = StartChild(
                    "client", "client-b", port, options.Scenario, childTimeout, options.Seed);
                clientB.BeginCaptureOutput();
                children.Add(clientB);

                Task exits = Task.WhenAll(children.Select(x => x.Process.WaitForExitAsync(cancellationToken)));
                Task deadline = Task.Delay(options.TimeoutMilliseconds, cancellationToken);
                Task completed = await Task.WhenAny(exits, deadline);
                if (completed != exits)
                {
                    controllerTimedOut = true;
                    controllerFailure = $"process-peer exceeded its {options.TimeoutMilliseconds}ms deadline.";
                }
                else
                {
                    await exits;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            controllerTimedOut = true;
            controllerFailure = "process-peer was cancelled.";
        }
        catch (Exception ex)
        {
            controllerFailure = $"controller-exception:{ex.GetType().Name}";
        }
        finally
        {
            orphanCleanupComplete = await CleanupChildrenAsync(children, cleanupFailures);
        }

        List<TransportNodeResult> nodeResults = new();
        ChildOutputCapture[] captures = await Task.WhenAll(children.Select(CaptureChildOutputAsync));
        foreach (ChildOutputCapture capture in captures)
        {
            TransportChildProcess child = capture.Child;
            if (!capture.Complete)
            {
                cleanupFailures.Add($"cleanup:{child.InstanceId}:output-capture-incomplete");
                continue;
            }

            child.StandardError = capture.StandardError;
            TransportNodeResult? nodeResult = TryParseNodeResult(capture.StandardOutput);
            if (nodeResult != null) nodeResults.Add(nodeResult);
        }

        try
        {
            ProcessPeerEvidence evidence = BuildEvidence(
                options,
                artifactManifest,
                port,
                startedAtUtc,
                children,
                nodeResults,
                controllerTimedOut,
                controllerFailure,
                orphanCleanupComplete,
                cleanupFailures);
            string json = JsonSerializer.Serialize(evidence, TransportJson.Options);
            if (options.OutputPath != null)
            {
                evidence.RequiredChecks["evidence-output-persisted"] = true;
                json = JsonSerializer.Serialize(evidence, TransportJson.Options);
                try
                {
                    await TransportEvidenceFileWriter.WriteAtomicallyAsync(options.OutputPath, json);
                }
                catch (Exception ex)
                {
                    evidence.RequiredChecks["evidence-output-persisted"] = false;
                    evidence.Failures.Add($"Evidence persistence failed: {ex.GetType().Name}.");
                    evidence.Verdict = "failed";
                }

                json = JsonSerializer.Serialize(evidence, TransportJson.Options);
            }

            await output.WriteLineAsync(json);
            await output.FlushAsync();

            if (controllerTimedOut) return TimeoutExitCode;
            return string.Equals(evidence.Verdict, "passed", StringComparison.Ordinal)
                ? 0
                : VerificationFailureExitCode;
        }
        finally
        {
            foreach (TransportChildProcess child in children) child.Dispose();
        }
    }

    private static ProcessPeerEvidence BuildEvidence(
        ProcessPeerOptions options,
        ProcessPeerArtifactManifest artifactManifest,
        int port,
        DateTime startedAtUtc,
        IReadOnlyList<TransportChildProcess> children,
        IReadOnlyList<TransportNodeResult> nodes,
        bool controllerTimedOut,
        string? controllerFailure,
        bool orphanCleanupComplete,
        IReadOnlyList<string> cleanupFailures)
    {
        var evidence = new ProcessPeerEvidence
        {
            Head = options.Head,
            Tree = options.Tree,
            Scenario = options.Scenario,
            Seed = options.Seed,
            ArtifactManifestSha256 = artifactManifest.ManifestDigest,
            ManifestRuntimeIdentity = artifactManifest.RuntimeIdentity,
            ControllerRuntimeIdentity = ProcessRuntimeIdentity.CaptureCurrent(),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            Topology = new ProcessPeerTopologyEvidence
            {
                Port = port,
                TransportVersion = GetRuntimePackageVersion("LiteNetLib"),
                TransportAssemblyVersion = GetAssemblyVersion(typeof(NetManager).Assembly)
            },
            Nodes = nodes.OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToList()
        };

        foreach (TransportChildProcess child in children)
        {
            evidence.Processes.Add(new TransportProcessEvidence
            {
                Role = child.Role,
                InstanceId = child.InstanceId,
                ProcessId = child.Process.Id,
                ExitCode = child.Process.HasExited ? child.Process.ExitCode : int.MinValue,
                Killed = child.Killed,
                StandardErrorBytes = Encoding.UTF8.GetByteCount(child.StandardError),
                StandardErrorSha256 = Sha256(child.StandardError)
            });
        }

        evidence.WireHashes = nodes
            .SelectMany(x => x.WireFrames)
            .Select(x => x.WireSha256)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        evidence.PayloadHashes = nodes
            .SelectMany(x => x.WireFrames)
            .Select(x => x.PayloadSha256)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        foreach (TransportNodeResult node in nodes)
        {
            if (node.LocalDigest != null)
            {
                evidence.Digest.ByInstance[node.InstanceId] = node.LocalDigest;
            }
        }

        evidence.Digest.Converged = evidence.Digest.ByInstance.Count == 3 &&
                                    evidence.Digest.ByInstance.Values.Distinct(StringComparer.Ordinal).Count() == 1;

        bool correctTopology = children.Count == 3 &&
                               children.Count(x => x.Role == "server") == 1 &&
                               children.Count(x => x.Role == "client") == 2;
        bool distinctProcesses = children.Select(x => x.Process.Id).Distinct().Count() == 3;
        bool nodeIdentitiesMatch = children.Count == 3 &&
                                   nodes.Count == 3 &&
                                   nodes.Select(x => x.InstanceId).Distinct(StringComparer.Ordinal).Count() == 3 &&
                                   children.All(child => nodes.Any(node =>
                                       node.InstanceId == child.InstanceId &&
                                       node.Role == child.Role &&
                                       node.ProcessId == child.Process.Id &&
                                       string.Equals(node.Seed, options.Seed, StringComparison.Ordinal)));
        bool cleanNodeExits = children.Count == 3 &&
                              children.All(x => x.Process.HasExited && x.Process.ExitCode == 0 && !x.Killed) &&
                              nodes.Count == 3 &&
                              nodes.All(x => x.Success);
        bool wireObserved = evidence.WireHashes.Count > 0 && evidence.PayloadHashes.Count > 0;

        evidence.RequiredChecks["clean-node-exits"] = cleanNodeExits;
        evidence.RequiredChecks["distinct-processes"] = distinctProcesses;
        evidence.RequiredChecks["exact-topology"] = correctTopology;
        evidence.RequiredChecks["node-process-identities"] = nodeIdentitiesMatch;
        evidence.RequiredChecks["orphan-cleanup"] = orphanCleanupComplete;
        evidence.RequiredChecks["wire-traffic"] = wireObserved;
        evidence.RequiredChecks["reliable-ordered-channel-zero"] =
            nodes.Count == 3 && nodes.All(node => node.DeliveryDomainObserved && node.DeliveryDomainValid);
        evidence.RequiredChecks["runtime-artifact-manifest-match"] =
            artifactManifest.ArtifactSetDigest.Length == 64 &&
            string.Equals(
                ProcessPeerArtifactManifestFile.CurrentArtifactSetDigest(),
                artifactManifest.ArtifactSetDigest,
                StringComparison.Ordinal);
        evidence.RequiredChecks["child-runtime-artifact-set-match"] =
            nodes.Count == 3 && nodes.All(node => string.Equals(
                node.RuntimeArtifactSetDigest,
                artifactManifest.ArtifactSetDigest,
                StringComparison.Ordinal));
        evidence.RequiredChecks["runtime-environment-manifest-match"] =
            evidence.ControllerRuntimeIdentity.HasValidShape() &&
            string.Equals(
                evidence.ControllerRuntimeIdentity.IdentityDigest,
                artifactManifest.RuntimeIdentity.IdentityDigest,
                StringComparison.Ordinal);
        evidence.RequiredChecks["child-runtime-environment-match"] =
            nodes.Count == 3 &&
            nodes.All(node =>
                node.RuntimeIdentity.HasValidShape() &&
                string.Equals(
                    node.RuntimeIdentity.IdentityDigest,
                    artifactManifest.RuntimeIdentity.IdentityDigest,
                    StringComparison.Ordinal));

        var hasher = new CanonicalJsonHasher();
        var orderedFrameManifest = nodes
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .Select(node => new
            {
                node.Role,
                node.InstanceId,
                node.Seed,
                node.DeliveryDomainObserved,
                node.DeliveryDomainValid,
                node.RuntimeArtifactSetDigest,
                RuntimeIdentityDigest = node.RuntimeIdentity.IdentityDigest,
                node.LocalState,
                Frames = node.WireFrames.Select(frame => new
                {
                    frame.Direction,
                    frame.Kind,
                    frame.InstanceId,
                    frame.Generation,
                    frame.Sequence,
                    frame.WireSha256,
                    frame.PayloadSha256
                }).ToArray()
            })
            .ToArray();
        evidence.ArtifactHashes["ordered-frame-manifest"] = hasher.ComputeSha256(orderedFrameManifest);
        evidence.ArtifactHashes["verification-harness-assembly"] = Sha256File(typeof(Program).Assembly.Location);
        evidence.ArtifactHashes["common-assembly"] = Sha256File(typeof(ProtoBufSerializer).Assembly.Location);
        evidence.ArtifactHashes["litenetlib-assembly"] = Sha256File(typeof(NetManager).Assembly.Location);
        evidence.ArtifactHashes["runtime-artifact-set"] = artifactManifest.ArtifactSetDigest;
        evidence.ArtifactHashes["runtime-artifact-manifest"] = artifactManifest.ManifestDigest;
        bool runtimeArtifactsHashed = evidence.ArtifactHashes
            .Where(item => item.Key.EndsWith("-assembly", StringComparison.Ordinal))
            .All(item => item.Value.Length == 64) &&
            artifactManifest.Artifacts.Count > 0 &&
            artifactManifest.Artifacts.Values.All(hash => hash.Length == 64) &&
            artifactManifest.RuntimeIdentity.HasValidShape() &&
            !string.IsNullOrWhiteSpace(evidence.Topology.TransportAssemblyVersion);
        evidence.RequiredChecks["runtime-artifacts-hashed"] = runtimeArtifactsHashed;
        evidence.RequiredChecks["loaded-transport-version"] = string.Equals(
            evidence.Topology.TransportVersion,
            TransportCodec.ExpectedLiteNetLibPackageVersion,
            StringComparison.Ordinal);

        if (TransportScenarios.IsNegativeProtocolCase(options.Scenario))
        {
            string expectedCode = TransportScenarios.ExpectedRejectionCode(options.Scenario)!;
            bool rejected = nodes.Any(x => x.InstanceId == "server" && x.RejectionCode == expectedCode) &&
                            nodes.Any(x => x.InstanceId == "client-b" && x.RejectionCode == expectedCode);
            evidence.RequiredChecks[$"{expectedCode}-rejected"] = rejected;
        }
        else
        {
            evidence.RequiredChecks["digest-convergence"] = evidence.Digest.Converged;
            Dictionary<string, TransportNodeResult>? nodesByInstance = nodeIdentitiesMatch
                ? nodes.ToDictionary(x => x.InstanceId, StringComparer.Ordinal)
                : null;
            bool acknowledgementsMatch = nodesByInstance != null &&
                                         nodesByInstance.TryGetValue("server", out TransportNodeResult? serverNode) &&
                                         new[] { "client-a", "client-b" }.All(instanceId =>
                                             nodesByInstance.TryGetValue(instanceId, out TransportNodeResult? clientNode) &&
                                             clientNode.LocalDigest is string localDigest &&
                                             serverNode.ObservedDigests.TryGetValue(instanceId, out string? observedDigest) &&
                                             string.Equals(localDigest, observedDigest, StringComparison.Ordinal));
            evidence.RequiredChecks["acknowledged-state-digests"] = acknowledgementsMatch;
        }

        if (options.Scenario == TransportScenarios.Reconnect)
        {
            bool reconnected = nodes.Any(x => x.InstanceId == "server" && x.CleanReconnectObserved) &&
                               nodes.Any(x => x.InstanceId == "client-b" && x.HighestGeneration == 2);
            evidence.RequiredChecks["clean-reconnect-generation"] = reconnected;
        }

        if (options.Scenario == TransportScenarios.Timeout)
        {
            evidence.RequiredChecks["completed-before-deadline"] = !controllerTimedOut;
        }

        if (controllerFailure != null) evidence.Failures.Add(controllerFailure);
        evidence.Failures.AddRange(cleanupFailures);
        foreach ((string check, bool passed) in evidence.RequiredChecks)
        {
            if (!passed) evidence.Failures.Add($"Required check failed: {check}.");
        }
        foreach (TransportProcessEvidence process in evidence.Processes)
        {
            if (process.StandardErrorBytes > 0)
            {
                evidence.Failures.Add(
                    $"{process.InstanceId} emitted {process.StandardErrorBytes} stderr bytes ({process.StandardErrorSha256}).");
            }
        }

        evidence.ArtifactHashes["wire-set"] = hasher.ComputeSha256(evidence.WireHashes);
        evidence.ArtifactHashes["payload-set"] = hasher.ComputeSha256(evidence.PayloadHashes);
        evidence.ReplayIdentity = hasher.ComputeSha256(new
        {
            options.Head,
            options.Tree,
            options.Scenario,
            options.Seed,
            OrderedFrameManifestSha256 = evidence.ArtifactHashes["ordered-frame-manifest"],
            VerificationHarnessAssemblySha256 = evidence.ArtifactHashes["verification-harness-assembly"],
            CommonAssemblySha256 = evidence.ArtifactHashes["common-assembly"],
            LiteNetLibAssemblySha256 = evidence.ArtifactHashes["litenetlib-assembly"],
            RuntimeArtifactSetSha256 = artifactManifest.ArtifactSetDigest,
            ArtifactManifestSha256 = artifactManifest.ManifestDigest,
            ManifestRuntimeIdentity = artifactManifest.RuntimeIdentity.IdentityDigest,
            ControllerRuntimeIdentity = evidence.ControllerRuntimeIdentity.IdentityDigest,
            ChildRuntimeIdentities = nodes
                .OrderBy(node => node.InstanceId, StringComparer.Ordinal)
                .Select(node => node.RuntimeIdentity.IdentityDigest)
                .ToArray()
        });
        evidence.Verdict = evidence.RequiredChecks.Values.All(x => x) && evidence.Failures.Count == 0
            ? "passed"
            : "failed";
        return evidence;
    }

    private static async Task<int> WriteArtifactManifestFailureAsync(
        ProcessPeerOptions options,
        TextWriter output,
        string failureType)
    {
        var evidence = new ProcessPeerEvidence
        {
            Head = options.Head,
            Tree = options.Tree,
            Scenario = options.Scenario,
            Seed = options.Seed,
            ControllerRuntimeIdentity = ProcessRuntimeIdentity.CaptureCurrent(),
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            Verdict = "failed"
        };
        evidence.RequiredChecks["runtime-artifact-manifest-match"] = false;
        evidence.Failures.Add($"Artifact manifest validation failed: {failureType}.");
        string json = JsonSerializer.Serialize(evidence, TransportJson.Options);
        if (options.OutputPath != null)
        {
            evidence.RequiredChecks["evidence-output-persisted"] = true;
            json = JsonSerializer.Serialize(evidence, TransportJson.Options);
            try
            {
                await TransportEvidenceFileWriter.WriteAtomicallyAsync(options.OutputPath, json);
            }
            catch (Exception ex)
            {
                evidence.RequiredChecks["evidence-output-persisted"] = false;
                evidence.Failures.Add($"Evidence persistence failed: {ex.GetType().Name}.");
            }

            json = JsonSerializer.Serialize(evidence, TransportJson.Options);
        }

        await output.WriteLineAsync(json);
        await output.FlushAsync();
        return VerificationFailureExitCode;
    }

    private static string GetAssemblyVersion(Assembly assembly)
    {
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? string.Empty
            : informational;
    }

    private static string GetRuntimePackageVersion(string packageName)
    {
        string dependencyManifest = Path.ChangeExtension(typeof(Program).Assembly.Location, ".deps.json");
        if (!File.Exists(dependencyManifest)) return string.Empty;

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(dependencyManifest));
        if (!document.RootElement.TryGetProperty("libraries", out JsonElement libraries)) return string.Empty;
        string prefix = packageName + "/";
        foreach (JsonProperty library in libraries.EnumerateObject())
        {
            if (library.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return library.Name.Substring(prefix.Length);
            }
        }

        return string.Empty;
    }

    private static string Sha256File(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static TransportChildProcess StartChild(
        string role,
        string instanceId,
        int port,
        string scenario,
        int timeoutMilliseconds,
        string seed)
    {
        string processHost = Environment.ProcessPath ??
            throw new InvalidOperationException("The process-peer controller host path is unavailable.");
        string hostAssembly = typeof(Program).Assembly.Location;
        string runtimeConfig = Path.ChangeExtension(hostAssembly, ".runtimeconfig.json");
        string dependencyManifest = Path.ChangeExtension(hostAssembly, ".deps.json");

        var startInfo = new ProcessStartInfo
        {
            FileName = processHost,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (string.Equals(
                Path.GetFileNameWithoutExtension(processHost),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(runtimeConfig);
            startInfo.ArgumentList.Add("--depsfile");
            startInfo.ArgumentList.Add(dependencyManifest);
            startInfo.ArgumentList.Add(hostAssembly);
        }
        startInfo.ArgumentList.Add("transport-node");
        AddOption(startInfo, "--role", role);
        AddOption(startInfo, "--instance-id", instanceId);
        AddOption(startInfo, "--port", port.ToString());
        AddOption(startInfo, "--scenario", scenario);
        AddOption(startInfo, "--timeout-ms", timeoutMilliseconds.ToString());
        AddOption(startInfo, "--seed", seed);

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start {instanceId} transport process.");
        }

        return new TransportChildProcess(role, instanceId, process);
    }

    private static void AddOption(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static async Task<string?> ReadLineBefore(
        StreamReader reader,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        try
        {
            return await reader.ReadLineAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static bool TryParseReady(string? line, int processId, out int port)
    {
        port = 0;
        if (line == null) return false;
        try
        {
            TransportReadyEvent? ready = JsonSerializer.Deserialize<TransportReadyEvent>(line, TransportJson.Options);
            if (ready == null ||
                ready.EventKind != "ready" ||
                ready.Role != "server" ||
                ready.ProcessId != processId ||
                ready.Port is < 1024 or > 65535)
            {
                return false;
            }

            port = ready.Port;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TransportNodeResult? TryParseNodeResult(string output)
    {
        foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            try
            {
                TransportNodeResult? result = JsonSerializer.Deserialize<TransportNodeResult>(line, TransportJson.Options);
                if (result?.EventKind == "node-result") return result;
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static int OptionsChildTimeout(ProcessPeerOptions options)
    {
        return options.Scenario == TransportScenarios.Timeout
            ? Math.Min(120000, options.TimeoutMilliseconds + 5000)
            : Math.Min(120000, options.TimeoutMilliseconds + 1500);
    }

    private static async Task<bool> CleanupChildrenAsync(
        IReadOnlyList<TransportChildProcess> children,
        ICollection<string> failures)
    {
        foreach (TransportChildProcess child in children)
        {
            if (child.Process.HasExited) continue;
            child.Killed = true;
            TryKill(child, entireProcessTree: true, failures);
        }

        bool[] firstWait = await Task.WhenAll(children.Select(child =>
            WaitForExitWithinAsync(child.Process, TimeSpan.FromSeconds(2))));
        if (firstWait.All(exited => exited)) return true;

        foreach (TransportChildProcess child in children)
        {
            if (child.Process.HasExited) continue;
            child.Killed = true;
            TryKill(child, entireProcessTree: false, failures);
        }

        bool[] finalWait = await Task.WhenAll(children.Select(child =>
            WaitForExitWithinAsync(child.Process, TimeSpan.FromSeconds(2))));
        return finalWait.All(exited => exited);
    }

    private static async Task<ChildOutputCapture> CaptureChildOutputAsync(TransportChildProcess child)
    {
        if (!child.Process.HasExited)
        {
            return new ChildOutputCapture(child, string.Empty, string.Empty, false);
        }

        Task<string> standardOutput = child.ReadCapturedOutputAsync();
        Task<string> standardError = child.StandardErrorTask;
        Task allOutput = Task.WhenAll(standardOutput, standardError);
        Task completed = await Task.WhenAny(allOutput, Task.Delay(TimeSpan.FromSeconds(1)));
        if (completed != allOutput)
        {
            return new ChildOutputCapture(child, string.Empty, string.Empty, false);
        }

        await allOutput;
        return new ChildOutputCapture(
            child,
            await standardOutput,
            await standardError,
            true);
    }

    private static void TryKill(
        TransportChildProcess child,
        bool entireProcessTree,
        ICollection<string> failures)
    {
        try
        {
            child.Process.Kill(entireProcessTree);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            failures.Add($"cleanup:{child.InstanceId}:kill:{ex.GetType().Name}");
        }
    }

    private static async Task<bool> WaitForExitWithinAsync(Process process, TimeSpan timeout)
    {
        if (process.HasExited) return true;
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            return process.HasExited;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return process.HasExited;
        }
    }
}

internal sealed class ChildOutputCapture
{
    public TransportChildProcess Child { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    public bool Complete { get; }

    public ChildOutputCapture(
        TransportChildProcess child,
        string standardOutput,
        string standardError,
        bool complete)
    {
        Child = child;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Complete = complete;
    }
}

internal static class TransportEvidenceFileWriter
{
    public static async Task WriteAtomicallyAsync(string outputPath, string json)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json + Environment.NewLine, CancellationToken.None);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }
}

internal sealed class ProcessPeerOptions
{
    public string Head { get; private set; } = string.Empty;
    public string Tree { get; private set; } = string.Empty;
    public string Scenario { get; private set; } = TransportScenarios.Converge;
    public int TimeoutMilliseconds { get; private set; } = 10000;
    public string Seed { get; private set; } = VerificationSeed.Default;
    public string ArtifactManifestPath { get; private set; } = string.Empty;
    public string? OutputPath { get; private set; }

    public static ProcessPeerOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("process-peer options must be --name <value> pairs.");
            }

            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Duplicate process-peer option: {args[index]}.");
            }
        }

        string head = Required(values, "--head");
        string tree = Required(values, "--tree");
        ValidateGitIdentity(head, "head");
        ValidateGitIdentity(tree, "tree");

        string scenario = values.GetValueOrDefault("--scenario", TransportScenarios.Converge);
        if (!TransportScenarios.IsKnown(scenario))
        {
            throw new ArgumentException($"Unknown process-peer scenario: {scenario}.");
        }

        if (!int.TryParse(values.GetValueOrDefault("--timeout-ms", "10000"), out int timeoutMilliseconds) ||
            timeoutMilliseconds is < 250 or > 120000)
        {
            throw new ArgumentException("process-peer timeout must be between 250 and 120000 milliseconds.");
        }

        string seed = VerificationSeed.Normalize(
            values.GetValueOrDefault("--seed", "1729"),
            "process-peer");

        string artifactManifestPath = Required(values, "--artifact-manifest");

        var known = new[]
        {
            "--head", "--tree", "--scenario", "--timeout-ms", "--seed", "--output", "--artifact-manifest"
        };
        string? unknown = values.Keys.FirstOrDefault(x => !known.Contains(x, StringComparer.Ordinal));
        if (unknown != null) throw new ArgumentException($"Unknown process-peer option: {unknown}.");

        return new ProcessPeerOptions
        {
            Head = head.ToLowerInvariant(),
            Tree = tree.ToLowerInvariant(),
            Scenario = scenario,
            TimeoutMilliseconds = timeoutMilliseconds,
            Seed = seed,
            ArtifactManifestPath = artifactManifestPath,
            OutputPath = values.GetValueOrDefault("--output")
        };
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string option)
    {
        if (!values.TryGetValue(option, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        return value;
    }

    private static void ValidateGitIdentity(string value, string name)
    {
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException($"process-peer {name} must be exactly 40 hexadecimal characters.");
        }
    }

}

internal sealed class TransportChildProcess : IDisposable
{
    private Task<string>? standardOutputTask;

    public string Role { get; }
    public string InstanceId { get; }
    public Process Process { get; }
    public bool Killed { get; set; }
    public string StandardError { get; set; } = string.Empty;
    public Task<string> StandardErrorTask { get; }

    public TransportChildProcess(string role, string instanceId, Process process)
    {
        Role = role;
        InstanceId = instanceId;
        Process = process;
        StandardErrorTask = process.StandardError.ReadToEndAsync();
    }

    public void BeginCaptureOutput()
    {
        standardOutputTask ??= Process.StandardOutput.ReadToEndAsync();
    }

    public Task<string> ReadCapturedOutputAsync()
    {
        BeginCaptureOutput();
        return standardOutputTask!;
    }

    public void Dispose()
    {
        Process.Dispose();
    }
}
