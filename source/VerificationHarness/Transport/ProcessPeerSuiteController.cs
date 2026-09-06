using System.Globalization;
using System.Text.Json;
using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public sealed class ProcessPeerSuiteController
{
    private static readonly string[] BaseScenarioCheckIds =
    {
        "child-runtime-artifact-set-match",
        "child-runtime-environment-match",
        "clean-node-exits",
        "distinct-processes",
        "exact-topology",
        "loaded-transport-version",
        "node-process-identities",
        "orphan-cleanup",
        "reliable-ordered-channel-zero",
        "runtime-artifact-manifest-match",
        "runtime-artifacts-hashed",
        "runtime-environment-manifest-match",
        "wire-traffic"
    };

    private static readonly ScenarioExpectation[] Expectations =
    {
        new(TransportScenarios.Converge, "convergence", ExpectedPass),
        new(TransportScenarios.Reconnect, "reconnect-generation", ExpectedPass),
        new(TransportScenarios.Malformed, "malformed-rejection", ExpectedPass),
        new(TransportScenarios.OutOfSequence, "out-of-sequence-rejection", ExpectedPass),
        new(TransportScenarios.CorruptAcknowledgement, "corrupt-ack-rejection", ExpectedPass),
        new(TransportScenarios.Diverge, "divergence-negative-control", ExpectedDivergence),
        new(TransportScenarios.Timeout, "deadline-cleanup-negative-control", ExpectedTimeout)
    };

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (args.Contains("--scenario", StringComparer.Ordinal))
        {
            throw new ArgumentException("process-peer-suite owns its scenario matrix and does not accept --scenario.");
        }

        ProcessPeerOptions options = ProcessPeerOptions.Parse(args);
        DateTime startedAtUtc = DateTime.UtcNow;
        var evidence = new ProcessPeerSuiteEvidence
        {
            Head = options.Head,
            Tree = options.Tree,
            Seed = options.Seed,
            StartedAtUtc = startedAtUtc
        };

        foreach (ScenarioExpectation expectation in Expectations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                evidence.Failures.Add("process-peer-suite-cancelled");
                break;
            }

            int timeoutMilliseconds = expectation.Scenario == TransportScenarios.Timeout
                ? 500
                : options.TimeoutMilliseconds;
            var scenarioOutput = new StringWriter(CultureInfo.InvariantCulture);
            int exitCode = await new ProcessPeerController().RunAsync(
                new[]
                {
                    "--head", options.Head,
                    "--tree", options.Tree,
                    "--scenario", expectation.Scenario,
                    "--timeout-ms", timeoutMilliseconds.ToString(CultureInfo.InvariantCulture),
                    "--seed", options.Seed,
                    "--artifact-manifest", options.ArtifactManifestPath
                },
                scenarioOutput,
                cancellationToken);

            ProcessPeerEvidence? scenarioEvidence = ParseScenarioEvidence(scenarioOutput.ToString());
            bool passed = scenarioEvidence != null &&
                          SourceMatches(options, expectation.Scenario, scenarioEvidence) &&
                          HasExpectedRequiredCheckCatalog(expectation.Scenario, scenarioEvidence.RequiredChecks) &&
                          expectation.Validate(exitCode, scenarioEvidence);
            evidence.RequiredChecks[expectation.CheckId] = passed;
            evidence.Scenarios.Add(new ProcessPeerScenarioEvidence
            {
                Scenario = expectation.Scenario,
                ExpectedOutcome = expectation.CheckId,
                ExitCode = exitCode,
                Passed = passed,
                Evidence = scenarioEvidence
            });
            if (!passed)
            {
                evidence.Failures.Add($"Scenario check failed: {expectation.CheckId}.");
            }
        }

        evidence.CompletedAtUtc = DateTime.UtcNow;
        ProcessPeerEvidence? baseline = evidence.Scenarios
            .Select(item => item.Evidence)
            .FirstOrDefault(item => item != null);
        if (baseline != null)
        {
            evidence.Topology.TransportVersion = baseline.Topology.TransportVersion;
            evidence.Topology.TransportAssemblyVersion = baseline.Topology.TransportAssemblyVersion;
            evidence.ArtifactManifestSha256 = baseline.ArtifactManifestSha256;
        }

        evidence.RequiredChecks["runtime-artifact-manifest-match"] =
            evidence.ArtifactManifestSha256.Length == 64 &&
            evidence.Scenarios.All(item =>
                item.Evidence != null &&
                string.Equals(
                    item.Evidence.ArtifactManifestSha256,
                    evidence.ArtifactManifestSha256,
                    StringComparison.Ordinal) &&
                item.Evidence.RequiredChecks.TryGetValue(
                    "runtime-artifact-manifest-match",
                    out bool manifestMatch) &&
                manifestMatch);
        if (!evidence.RequiredChecks["runtime-artifact-manifest-match"])
        {
            evidence.Failures.Add("Required check failed: runtime-artifact-manifest-match.");
        }

        evidence.RequiredChecks["child-runtime-artifact-set-match"] =
            evidence.Scenarios
                .Where(item => item.Scenario != TransportScenarios.Timeout)
                .All(item => item.Evidence != null &&
                    item.Evidence.RequiredChecks.TryGetValue(
                        "child-runtime-artifact-set-match",
                        out bool childRuntimeMatch) &&
                    childRuntimeMatch);
        if (!evidence.RequiredChecks["child-runtime-artifact-set-match"])
        {
            evidence.Failures.Add("Required check failed: child-runtime-artifact-set-match.");
        }

        evidence.RequiredChecks["runtime-environment-manifest-match"] =
            evidence.Scenarios.All(item =>
                item.Evidence != null &&
                item.Evidence.RequiredChecks.TryGetValue(
                    "runtime-environment-manifest-match",
                    out bool runtimeEnvironmentMatch) &&
                runtimeEnvironmentMatch);
        if (!evidence.RequiredChecks["runtime-environment-manifest-match"])
        {
            evidence.Failures.Add("Required check failed: runtime-environment-manifest-match.");
        }

        evidence.RequiredChecks["child-runtime-environment-match"] =
            evidence.Scenarios
                .Where(item => item.Scenario != TransportScenarios.Timeout)
                .All(item => item.Evidence != null &&
                    item.Evidence.RequiredChecks.TryGetValue(
                        "child-runtime-environment-match",
                        out bool childRuntimeEnvironmentMatch) &&
                    childRuntimeEnvironmentMatch);
        if (!evidence.RequiredChecks["child-runtime-environment-match"])
        {
            evidence.Failures.Add("Required check failed: child-runtime-environment-match.");
        }

        evidence.TotalChildProcessCount = evidence.Scenarios
            .Where(item => item.Evidence != null)
            .Sum(item => item.Evidence!.Processes.Count);
        var hasher = new CanonicalJsonHasher();
        evidence.ArtifactHashes["scenario-replay-set"] = hasher.ComputeSha256(
            evidence.Scenarios.Select(item => new
            {
                item.Scenario,
                item.ExpectedOutcome,
                item.ExitCode,
                item.Passed,
                ReplayIdentity = item.Evidence?.ReplayIdentity ?? string.Empty
            }).ToArray());
        if (!string.IsNullOrWhiteSpace(evidence.ArtifactManifestSha256))
        {
            evidence.ArtifactHashes["runtime-artifact-manifest"] = evidence.ArtifactManifestSha256;
        }
        FinalizeEvidence(evidence, options, hasher);

        string json = JsonSerializer.Serialize(evidence, TransportJson.Options);
        if (options.OutputPath != null)
        {
            evidence.RequiredChecks["evidence-output-persisted"] = true;
            FinalizeEvidence(evidence, options, hasher);
            json = JsonSerializer.Serialize(evidence, TransportJson.Options);
            try
            {
                await TransportEvidenceFileWriter.WriteAtomicallyAsync(options.OutputPath, json);
            }
            catch (Exception ex)
            {
                evidence.RequiredChecks["evidence-output-persisted"] = false;
                evidence.Failures.Add($"Evidence persistence failed: {ex.GetType().Name}.");
                FinalizeEvidence(evidence, options, hasher);
                json = JsonSerializer.Serialize(evidence, TransportJson.Options);
            }
        }

        await output.WriteLineAsync(json);
        await output.FlushAsync();
        if (cancellationToken.IsCancellationRequested) return ProcessPeerController.TimeoutExitCode;
        return string.Equals(evidence.Verdict, "passed", StringComparison.Ordinal)
            ? 0
            : ProcessPeerController.VerificationFailureExitCode;
    }

    private static void FinalizeEvidence(
        ProcessPeerSuiteEvidence evidence,
        ProcessPeerOptions options,
        CanonicalJsonHasher hasher)
    {
        evidence.StateDigest = hasher.ComputeSha256(new
        {
            profile = evidence.Profile,
            evidence.RequiredChecks,
            failures = evidence.Failures.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            scenarios = evidence.Scenarios.Select(item => new
            {
                item.Scenario,
                item.ExpectedOutcome,
                item.Passed,
                verdict = item.Evidence?.Verdict ?? "missing",
                digestConverged = item.Evidence?.Digest.Converged ?? false,
                localDigests = item.Evidence == null
                    ? Array.Empty<KeyValuePair<string, string>>()
                    : item.Evidence.Digest.ByInstance
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .ToArray()
            }).ToArray()
        });
        evidence.ReplayIdentity = hasher.ComputeSha256(new
        {
            options.Head,
            options.Tree,
            options.Seed,
            ScenarioReplaySetSha256 = evidence.ArtifactHashes["scenario-replay-set"],
            evidence.ArtifactManifestSha256
        });
        evidence.Verdict = evidence.RequiredChecks.Count >= Expectations.Length &&
                           evidence.RequiredChecks.Values.All(value => value) &&
                           evidence.Failures.Count == 0
            ? "passed"
            : "failed";
    }

    private static ProcessPeerEvidence? ParseScenarioEvidence(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProcessPeerEvidence>(json.Trim(), TransportJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static bool SourceMatches(
        ProcessPeerOptions options,
        string expectedScenario,
        ProcessPeerEvidence evidence)
    {
        return string.Equals(evidence.SchemaVersion, "process-peer.evidence.v1", StringComparison.Ordinal) &&
               string.Equals(evidence.Profile, "process-peer", StringComparison.Ordinal) &&
               string.Equals(evidence.Tier, "process-peer", StringComparison.Ordinal) &&
               string.Equals(evidence.EvidenceProfile, "functional", StringComparison.Ordinal) &&
               string.Equals(expectedScenario, evidence.Scenario, StringComparison.Ordinal) &&
               string.Equals(options.Head, evidence.Head, StringComparison.Ordinal) &&
               string.Equals(options.Tree, evidence.Tree, StringComparison.Ordinal) &&
               string.Equals(options.Seed, evidence.Seed, StringComparison.Ordinal) &&
               evidence.ArtifactManifestSha256.Length == 64 &&
               evidence.RequiredChecks.TryGetValue("runtime-artifact-manifest-match", out bool manifestMatch) &&
               manifestMatch;
    }

    private static bool ExpectedPass(int exitCode, ProcessPeerEvidence evidence)
    {
        return exitCode == 0 && string.Equals(evidence.Verdict, "passed", StringComparison.Ordinal);
    }

    private static bool ExpectedDivergence(int exitCode, ProcessPeerEvidence evidence)
    {
        return exitCode == ProcessPeerController.VerificationFailureExitCode &&
               string.Equals(evidence.Verdict, "failed", StringComparison.Ordinal) &&
               evidence.RequiredChecks.TryGetValue("digest-convergence", out bool converged) &&
               !converged &&
               evidence.RequiredChecks.TryGetValue("acknowledged-state-digests", out bool acknowledgementsMatch) &&
               acknowledgementsMatch &&
               HasOnlyExpectedFailures(evidence, new[] { "digest-convergence" });
    }

    private static bool ExpectedTimeout(int exitCode, ProcessPeerEvidence evidence)
    {
        return exitCode == ProcessPeerController.TimeoutExitCode &&
               string.Equals(evidence.Verdict, "failed", StringComparison.Ordinal) &&
               evidence.RequiredChecks.TryGetValue("completed-before-deadline", out bool completed) &&
               !completed &&
               evidence.RequiredChecks.TryGetValue("orphan-cleanup", out bool cleanupComplete) &&
               cleanupComplete &&
               evidence.Processes.Count == 3 &&
               evidence.Processes.All(process => process.Killed) &&
               HasOnlyExpectedFailures(
                   evidence,
                   new[]
                   {
                       "acknowledged-state-digests",
                       "child-runtime-artifact-set-match",
                       "child-runtime-environment-match",
                       "clean-node-exits",
                       "completed-before-deadline",
                       "digest-convergence",
                       "node-process-identities",
                       "reliable-ordered-channel-zero",
                       "wire-traffic"
                   },
                   "process-peer exceeded its ");
    }

    internal static bool HasExpectedRequiredCheckCatalog(
        string scenario,
        IReadOnlyDictionary<string, bool> requiredChecks)
    {
        if (requiredChecks == null) throw new ArgumentNullException(nameof(requiredChecks));
        string[] expected = ExpectedRequiredCheckIds(scenario);
        string[] actual = requiredChecks.Keys
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    internal static string[] ExpectedRequiredCheckIds(string scenario)
    {
        string[] scenarioCheckIds = scenario switch
        {
            TransportScenarios.Converge or TransportScenarios.Diverge =>
                new[] { "acknowledged-state-digests", "digest-convergence" },
            TransportScenarios.Reconnect =>
                new[] { "acknowledged-state-digests", "clean-reconnect-generation", "digest-convergence" },
            TransportScenarios.Malformed =>
                new[] { "malformed-frame-rejected" },
            TransportScenarios.OutOfSequence =>
                new[] { "invalid-sequence-rejected" },
            TransportScenarios.CorruptAcknowledgement =>
                new[] { "digest-mismatch-rejected" },
            TransportScenarios.Timeout =>
                new[] { "acknowledged-state-digests", "completed-before-deadline", "digest-convergence" },
            _ => Array.Empty<string>()
        };
        if (scenarioCheckIds.Length == 0) return Array.Empty<string>();

        return BaseScenarioCheckIds
            .Concat(scenarioCheckIds)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasOnlyExpectedFailures(
        ProcessPeerEvidence evidence,
        IReadOnlyCollection<string> expectedFailedChecks,
        string? allowedControllerFailurePrefix = null)
    {
        string[] actualFailedChecks = evidence.RequiredChecks
            .Where(item => !item.Value)
            .Select(item => item.Key)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectedFailedChecks
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (!actualFailedChecks.SequenceEqual(expected, StringComparer.Ordinal)) return false;

        var expectedFailureMessages = actualFailedChecks
            .Select(check => $"Required check failed: {check}.")
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedFailureMessages.All(message => evidence.Failures.Contains(message, StringComparer.Ordinal)))
            return false;

        return evidence.Failures.All(failure =>
            expectedFailureMessages.Contains(failure) ||
            (allowedControllerFailurePrefix != null &&
             failure.StartsWith(allowedControllerFailurePrefix, StringComparison.Ordinal)));
    }

    private sealed class ScenarioExpectation
    {
        public string Scenario { get; }
        public string CheckId { get; }
        public Func<int, ProcessPeerEvidence, bool> Validate { get; }

        public ScenarioExpectation(
            string scenario,
            string checkId,
            Func<int, ProcessPeerEvidence, bool> validate)
        {
            Scenario = scenario;
            CheckId = checkId;
            Validate = validate;
        }
    }
}

public sealed class ProcessPeerSuiteEvidence
{
    public string SchemaVersion { get; set; } = "process-peer-suite.evidence.v1";
    public string Profile { get; set; } = "process-peer";
    public string Tier { get; set; } = "process-peer";
    public string EvidenceProfile { get; set; } = "functional";
    public string Head { get; set; } = string.Empty;
    public string Tree { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
    public string ArtifactManifestSha256 { get; set; } = string.Empty;
    public string Verdict { get; set; } = "failed";
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public ProcessPeerTopologyEvidence Topology { get; set; } = new();
    public int MaximumConcurrentChildProcessCount { get; set; } = 3;
    public int TotalChildProcessCount { get; set; }
    public SortedDictionary<string, bool> RequiredChecks { get; set; } = new(StringComparer.Ordinal);
    public string StateDigest { get; set; } = string.Empty;
    public string ReplayIdentity { get; set; } = string.Empty;
    public SortedDictionary<string, string> ArtifactHashes { get; set; } = new(StringComparer.Ordinal);
    public List<ProcessPeerScenarioEvidence> Scenarios { get; set; } = new();
    public List<string> Failures { get; set; } = new();
}

public sealed class ProcessPeerScenarioEvidence
{
    public string Scenario { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool Passed { get; set; }
    public ProcessPeerEvidence? Evidence { get; set; }
}
