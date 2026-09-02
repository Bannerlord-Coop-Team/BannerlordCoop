using System.Text.Json;
using VerificationHarness.Serialization;

namespace VerificationHarness.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticController
{
    public const int BlockedExitCode = 8;

    private readonly IDedicatedServerControlClient controlClient;
    private readonly IDedicatedServerControlResponseValidator responseValidator;
    private readonly ICanonicalJsonHasher hasher;

    public DedicatedServerSyntheticController()
        : this(
            new NamedPipeDedicatedServerControlClient(),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher())
    {
    }

    public DedicatedServerSyntheticController(
        IDedicatedServerControlClient controlClient,
        IDedicatedServerControlResponseValidator responseValidator,
        ICanonicalJsonHasher hasher)
    {
        if (controlClient == null) throw new ArgumentNullException(nameof(controlClient));
        if (responseValidator == null) throw new ArgumentNullException(nameof(responseValidator));
        if (hasher == null) throw new ArgumentNullException(nameof(hasher));
        this.controlClient = controlClient;
        this.responseValidator = responseValidator;
        this.hasher = hasher;
    }

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (output == null) throw new ArgumentNullException(nameof(output));

        DedicatedServerSyntheticOptions options = DedicatedServerSyntheticOptions.Parse(args);
        DateTime startedAtUtc = DateTime.UtcNow;
        DedicatedServerControlValidation validation;
        try
        {
            string responseJson = await controlClient.GetStatusAsync(
                options.ServerProcessId,
                options.RequestId,
                TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),
                cancellationToken);
            validation = responseValidator.Validate(
                responseJson,
                new DedicatedServerControlExpectation(
                    options.ServerProcessId,
                    options.RunToken,
                    options.RequestId,
                    options.JoinPort,
                    DedicatedServerSyntheticOptions.ExpectedControllerIds));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            validation = FailureValidation("control-timeout");
        }
        catch (Exception)
        {
            // Local paths, pipe details, and any environment-derived values stay out of evidence.
            validation = FailureValidation("control-preflight-failed");
        }

        DedicatedServerSyntheticEvidence evidence = BuildEvidence(
            options,
            startedAtUtc,
            validation);
        string json;
        if (options.OutputPath != null)
        {
            evidence.RequiredChecks["evidence-output-persisted"] = true;
            SetStateDigest(evidence);
            json = JsonSerializer.Serialize(evidence, DedicatedServerSyntheticJson.Options);
            try
            {
                await VerificationHarness.Transport.TransportEvidenceFileWriter.WriteAtomicallyAsync(
                    options.OutputPath,
                    json);
            }
            catch (Exception)
            {
                evidence.RequiredChecks["evidence-output-persisted"] = false;
                evidence.Failures.Add("evidence-output-persistence-failed");
                evidence.Failures = evidence.Failures
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();
                SetStateDigest(evidence);
            }
        }
        else
        {
            SetStateDigest(evidence);
        }

        json = JsonSerializer.Serialize(evidence, DedicatedServerSyntheticJson.Options);
        await output.WriteLineAsync(json);
        await output.FlushAsync();

        return BlockedExitCode;
    }

    private DedicatedServerSyntheticEvidence BuildEvidence(
        DedicatedServerSyntheticOptions options,
        DateTime startedAtUtc,
        DedicatedServerControlValidation validation)
    {
        var evidence = new DedicatedServerSyntheticEvidence
        {
            Seed = options.Seed,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            CoopSource = new DedicatedServerSyntheticSourceEvidence
            {
                Head = options.Head,
                Tree = options.Tree
            },
            DedicatedServerSource = new DedicatedServerSyntheticSourceEvidence
            {
                Head = options.ServerHead,
                Tree = options.ServerTree
            },
            Topology = new DedicatedServerSyntheticTopologyEvidence
            {
                ServerProcessId = options.ServerProcessId,
                JoinPort = options.JoinPort
            }
        };

        evidence.RequiredChecks["control-envelope"] = validation.EnvelopeValid;
        evidence.RequiredChecks["control-request-identity"] = validation.RequestIdentityValid;
        evidence.RequiredChecks["dedicated-process-identity"] = validation.ProcessIdentityValid;
        evidence.RequiredChecks["dedicated-server-serving"] = validation.ServingValid;
        evidence.RequiredChecks["join-port"] = validation.JoinPortValid;
        evidence.RequiredChecks["first-class-connection-roster"] = validation.RosterSurfaceValid;
        evidence.RequiredChecks["exact-two-client-roster"] = validation.ExpectedRosterValid;
        evidence.RequiredChecks["runtime-scenario-executed"] = false;

        evidence.Failures.AddRange(validation.FailureCodes.Distinct(StringComparer.Ordinal));
        if (!validation.RosterSurfaceValid)
        {
            evidence.Failures.Add("blocked-on-dedicated-server-roster-surface");
        }

        // This foundation intentionally cannot pass until an actual DS process has been driven
        // through the complete scenario by the later runtime controller.
        evidence.Failures.Add("runtime-scenario-controller-not-implemented");
        evidence.Failures = evidence.Failures
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        evidence.ArtifactHashes["wire-manifest"] = DedicatedServerWireManifest.Sha256;
        evidence.ReplayIdentity = hasher.ComputeSha256(new
        {
            profile = evidence.Profile,
            coopSource = new { options.Head, options.Tree },
            dedicatedServerSource = new { head = options.ServerHead, tree = options.ServerTree },
            options.Seed,
            options.TimeoutMilliseconds,
            expectedClientCount = 2,
            expectedControllerIds = DedicatedServerSyntheticOptions.ExpectedControllerIds,
            manifestVersion = DedicatedServerWireManifest.Version,
            manifestSha256 = DedicatedServerWireManifest.Sha256
        });
        evidence.Verdict = "blocked";
        return evidence;
    }

    private void SetStateDigest(DedicatedServerSyntheticEvidence evidence)
    {
        evidence.StateDigest = hasher.ComputeSha256(new
        {
            profile = evidence.Profile,
            verdict = evidence.Verdict,
            evidence.RequiredChecks,
            evidence.Failures,
            expectedControllerIds = DedicatedServerSyntheticOptions.ExpectedControllerIds,
            manifestSha256 = DedicatedServerWireManifest.Sha256
        });
    }

    private static DedicatedServerControlValidation FailureValidation(string failureCode)
    {
        return new DedicatedServerControlValidation
        {
            FailureCodes = new List<string> { failureCode }
        };
    }
}

public sealed class DedicatedServerSyntheticOptions
{
    public static IReadOnlyList<string> ExpectedControllerIds { get; } = Array.AsReadOnly(new[]
    {
        "ds-synthetic-client-a",
        "ds-synthetic-client-b"
    });

    public string Head { get; private set; } = string.Empty;
    public string Tree { get; private set; } = string.Empty;
    public string ServerHead { get; private set; } = string.Empty;
    public string ServerTree { get; private set; } = string.Empty;
    public int ServerProcessId { get; private set; }
    public string RunToken { get; private set; } = string.Empty;
    public string RequestId { get; private set; } = string.Empty;
    public int JoinPort { get; private set; }
    public int TimeoutMilliseconds { get; private set; } = 5000;
    public string Seed { get; private set; } = VerificationSeed.Default;
    public string? OutputPath { get; private set; }

    public static DedicatedServerSyntheticOptions Parse(string[] args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        var values = ParsePairs(args);
        string head = RequireCommit(values, "--head");
        string tree = RequireCommit(values, "--tree");
        string serverHead = RequireCommit(values, "--server-head");
        string serverTree = RequireCommit(values, "--server-tree");
        int serverProcessId = RequireInt(values, "--server-pid", 1, int.MaxValue);
        string runToken = RequireToken(values, "--run-token", 64);
        string requestId = RequireToken(values, "--request-id", 128);
        int joinPort = RequireInt(values, "--join-port", 1024, 65535);
        int timeoutMilliseconds = OptionalInt(values, "--timeout-ms", 5000, 250, 30000);
        string seed = VerificationSeed.Normalize(
            values.GetValueOrDefault("--seed", "1729"),
            "dedicated-server-synthetic");
        values.TryGetValue("--output", out string? outputPath);
        if (outputPath != null && string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("The --output path cannot be empty.");
        }

        string[] known =
        {
            "--head", "--tree", "--server-head", "--server-tree", "--server-pid",
            "--run-token", "--request-id", "--join-port", "--timeout-ms", "--seed", "--output"
        };
        string? unknown = values.Keys.FirstOrDefault(x => !known.Contains(x, StringComparer.Ordinal));
        if (unknown != null) throw new ArgumentException($"Unknown dedicated-server-synthetic option: {unknown}.");

        return new DedicatedServerSyntheticOptions
        {
            Head = head,
            Tree = tree,
            ServerHead = serverHead,
            ServerTree = serverTree,
            ServerProcessId = serverProcessId,
            RunToken = runToken,
            RequestId = requestId,
            JoinPort = joinPort,
            TimeoutMilliseconds = timeoutMilliseconds,
            Seed = seed,
            OutputPath = outputPath
        };
    }

    private static Dictionary<string, string> ParsePairs(string[] args)
    {
        if (args.Length % 2 != 0)
        {
            throw new ArgumentException("Dedicated-server-synthetic options must be --name <value> pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Invalid or duplicate option: {args[index]}.");
            }
        }

        return values;
    }

    private static string RequireCommit(IReadOnlyDictionary<string, string> values, string option)
    {
        string value = Required(values, option);
        if (value.Length != 40 || value.Any(x => !Uri.IsHexDigit(x)))
        {
            throw new ArgumentException($"{option} must be an exact 40-character hexadecimal identity.");
        }

        return value.ToLowerInvariant();
    }

    private static string RequireToken(
        IReadOnlyDictionary<string, string> values,
        string option,
        int maximumLength)
    {
        string value = Required(values, option);
        if (value.Length > maximumLength ||
            value.Any(x => !char.IsLetterOrDigit(x) && x is not '_' and not '-' and not '.'))
        {
            throw new ArgumentException(
                $"{option} must contain 1-{maximumLength} letters, digits, periods, underscores, or hyphens.");
        }

        return value;
    }

    private static int RequireInt(
        IReadOnlyDictionary<string, string> values,
        string option,
        int minimum,
        int maximum)
    {
        string value = Required(values, option);
        if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"{option} must be between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private static int OptionalInt(
        IReadOnlyDictionary<string, string> values,
        string option,
        int defaultValue,
        int minimum,
        int maximum)
    {
        return values.ContainsKey(option)
            ? RequireInt(values, option, minimum, maximum)
            : defaultValue;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string option)
    {
        if (!values.TryGetValue(option, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        return value;
    }
}

internal static class DedicatedServerSyntheticJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
