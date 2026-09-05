namespace VerificationHarness.Planning;

public sealed class VerificationPlan
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public VerificationSourceIdentity Source { get; }
    public string Decision { get; }
    public string Verdict { get; }
    public bool InputValid { get; }
    public string HighestRequiredTier { get; }
    public string Seed { get; }
    public string PlanDigest { get; }
    public string ReplayIdentity { get; }
    public DateTimeOffset? StartedAtUtc { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
    public IReadOnlyList<ArtifactHash> ArtifactHashes { get; }
    public IReadOnlyList<ProcessExitIdentity> ProcessExits { get; }
    public IReadOnlyList<string> ChangedPaths { get; }
    public IReadOnlyList<RejectedPath> RejectedPaths { get; }
    public IReadOnlyList<string> RequiredTiers { get; }
    public IReadOnlyList<string> RequiredChecks { get; }
    public IReadOnlyList<VerificationProfile> Profiles { get; }
    public IReadOnlyList<VerificationCheck> Checks { get; }
    public IReadOnlyList<VerificationReason> Reasons { get; }

    public VerificationPlan(
        VerificationSourceIdentity source,
        string decision,
        string verdict,
        bool inputValid,
        string highestRequiredTier,
        string seed,
        string planDigest,
        string replayIdentity,
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<RejectedPath> rejectedPaths,
        IReadOnlyList<string> requiredTiers,
        IReadOnlyList<string> requiredChecks,
        IReadOnlyList<VerificationProfile> profiles,
        IReadOnlyList<VerificationCheck> checks,
        IReadOnlyList<VerificationReason> reasons)
    {
        SchemaVersion = CurrentSchemaVersion;
        Source = source;
        Decision = decision;
        Verdict = verdict;
        InputValid = inputValid;
        HighestRequiredTier = highestRequiredTier;
        Seed = seed;
        PlanDigest = planDigest;
        ReplayIdentity = replayIdentity;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        ArtifactHashes = Array.Empty<ArtifactHash>();
        ProcessExits = Array.Empty<ProcessExitIdentity>();
        ChangedPaths = changedPaths;
        RejectedPaths = rejectedPaths;
        RequiredTiers = requiredTiers;
        RequiredChecks = requiredChecks;
        Profiles = profiles;
        Checks = checks;
        Reasons = reasons;
    }
}

public sealed class VerificationSourceIdentity
{
    public string Head { get; }
    public string SyntheticTree { get; }

    public VerificationSourceIdentity(string head, string syntheticTree)
    {
        Head = NormalizeGitObjectId(head, nameof(head));
        SyntheticTree = NormalizeGitObjectId(syntheticTree, nameof(syntheticTree));
    }

    private static string NormalizeGitObjectId(string value, string parameterName)
    {
        if (value == null) throw new ArgumentNullException(parameterName);
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Git object ids must contain exactly 40 hexadecimal characters.", parameterName);
        }

        return value.ToLowerInvariant();
    }
}

public sealed class RejectedPath
{
    public string? SuppliedPath { get; }
    public string Reason { get; }

    public RejectedPath(string? suppliedPath, string reason)
    {
        SuppliedPath = suppliedPath;
        Reason = reason;
    }
}

public sealed class VerificationProfile
{
    public string Id { get; }
    public string Tier { get; }
    public int Ordinal { get; }
    public bool Required { get; }
    public bool Blocking { get; }
    public string Verdict { get; }
    public string Executor { get; }
    public string Action { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string Scope { get; }

    public VerificationProfile(
        string id,
        int ordinal,
        bool required,
        string verdict,
        string executor,
        string action,
        IReadOnlyList<string> arguments,
        string scope)
    {
        Id = id;
        Tier = id;
        Ordinal = ordinal;
        Required = required;
        Blocking = true;
        Verdict = verdict;
        Executor = executor;
        Action = action;
        Arguments = arguments;
        Scope = scope;
    }
}

public sealed class VerificationCheck
{
    public string Id { get; }
    public string Profile { get; }
    public string Tier { get; }
    public bool Required { get; }
    public bool Blocking { get; }
    public string Verdict { get; }
    public VerificationTopology Topology { get; }
    public string Seed { get; }
    public string? StateDigest { get; }
    public string ReplayIdentity { get; }
    public DateTimeOffset? StartedAtUtc { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
    public IReadOnlyList<ArtifactHash> ArtifactHashes { get; }
    public IReadOnlyList<ProcessExitIdentity> ProcessExits { get; }
    public string? EvidenceProfile { get; }

    public VerificationCheck(
        string id,
        string profile,
        bool required,
        string verdict,
        VerificationTopology topology,
        string seed,
        string replayIdentity,
        string? evidenceProfile)
    {
        Id = id;
        Profile = profile;
        Tier = profile;
        Required = required;
        Blocking = true;
        Verdict = verdict;
        Topology = topology;
        Seed = seed;
        StateDigest = null;
        ReplayIdentity = replayIdentity;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        ArtifactHashes = Array.Empty<ArtifactHash>();
        ProcessExits = Array.Empty<ProcessExitIdentity>();
        EvidenceProfile = evidenceProfile;
    }
}

public sealed class VerificationTopology
{
    public int ServerCount { get; }
    public int ClientCount { get; }
    public int ProcessCount { get; }
    public bool ProcessIsolated { get; }

    public VerificationTopology(int serverCount, int clientCount, int processCount, bool processIsolated)
    {
        ServerCount = serverCount;
        ClientCount = clientCount;
        ProcessCount = processCount;
        ProcessIsolated = processIsolated;
    }
}

public sealed class ArtifactHash
{
    public string Path { get; }
    public string Algorithm { get; }
    public string Digest { get; }

    public ArtifactHash(string path, string algorithm, string digest)
    {
        Path = path;
        Algorithm = algorithm;
        Digest = digest;
    }
}

public sealed class ProcessExitIdentity
{
    public string InstanceId { get; }
    public int ProcessId { get; }
    public int ExitCode { get; }

    public ProcessExitIdentity(string instanceId, int processId, int exitCode)
    {
        InstanceId = instanceId;
        ProcessId = processId;
        ExitCode = exitCode;
    }
}

public sealed class VerificationReason
{
    public string RuleId { get; }
    public string Tier { get; }
    public string Description { get; }
    public IReadOnlyList<string> Paths { get; }

    public VerificationReason(string ruleId, string tier, string description, IReadOnlyList<string> paths)
    {
        RuleId = ruleId;
        Tier = tier;
        Description = description;
        Paths = paths;
    }
}
