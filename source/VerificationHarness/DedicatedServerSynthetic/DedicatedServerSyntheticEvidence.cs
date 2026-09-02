namespace VerificationHarness.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticSourceEvidence
{
    public string Head { get; set; } = string.Empty;
    public string Tree { get; set; } = string.Empty;
}

public sealed class DedicatedServerSyntheticTopologyEvidence
{
    public string Transport { get; set; } = "LiteNetLib";
    public string TransportVersion { get; set; } = "1.3.1";
    public int ChannelsCount { get; set; } = 2;
    public int DisconnectTimeoutMilliseconds { get; set; } = 60000;
    public int UpdateTimeMilliseconds { get; set; } = 15;
    public int MaximumPollMilliseconds { get; set; } = 25;
    public int ServerProcessId { get; set; }
    public int JoinPort { get; set; }
    public int ExpectedClientCount { get; set; } = 2;
}

public sealed class DedicatedServerSyntheticManifestEvidence
{
    public string Version { get; set; } = DedicatedServerWireManifest.Version;
    public string Sha256 { get; set; } = DedicatedServerWireManifest.Sha256;
    public IReadOnlyList<DedicatedServerWireEntry> Entries { get; set; } =
        DedicatedServerWireManifest.Entries;
}

public sealed class DedicatedServerSyntheticEvidence
{
    public string SchemaVersion { get; set; } = "dedicated-server-synthetic.evidence.v1";
    public string Profile { get; set; } = "dedicated-server-synthetic";
    public string Tier { get; set; } = "dedicated-server-synthetic";
    public string EvidenceProfile { get; set; } = "functional";
    public string Verdict { get; set; } = "blocked";
    public string Seed { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public DedicatedServerSyntheticSourceEvidence CoopSource { get; set; } = new();
    public DedicatedServerSyntheticSourceEvidence DedicatedServerSource { get; set; } = new();
    public DedicatedServerSyntheticTopologyEvidence Topology { get; set; } = new();
    public DedicatedServerSyntheticManifestEvidence WireManifest { get; set; } = new();
    public SortedDictionary<string, bool> RequiredChecks { get; set; } = new(StringComparer.Ordinal);
    public string StateDigest { get; set; } = string.Empty;
    public string ReplayIdentity { get; set; } = string.Empty;
    public SortedDictionary<string, string> ArtifactHashes { get; set; } = new(StringComparer.Ordinal);
    public List<string> Failures { get; set; } = new();
}

public sealed class DedicatedServerSyntheticNodeResult
{
    public string SchemaVersion { get; set; } = "dedicated-server-synthetic.node.v1";
    public string EventKind { get; set; } = "node-result";
    public string Role { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string RunToken { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool PasswordConfigured { get; set; }
    public bool Success { get; set; }
    public int AcceptedConnections { get; set; }
    public int RejectedPasswords { get; set; }
    public int Disconnections { get; set; }
    public int HeartbeatsObserved { get; set; }
    public int ModuleDenialsObserved { get; set; }
    public int FreshControllerResultsObserved { get; set; }
    public bool ProtocolShortcut { get; set; }
    public List<string> WireHashes { get; set; } = new();
    public List<string> FailureCodes { get; set; } = new();
}

public sealed class DedicatedServerSyntheticReadyEvent
{
    public string SchemaVersion { get; set; } = "dedicated-server-synthetic.node.v1";
    public string EventKind { get; set; } = "ready";
    public string Role { get; set; } = "server";
    public string RequestId { get; set; } = string.Empty;
    public string RunToken { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public int Port { get; set; }
}
