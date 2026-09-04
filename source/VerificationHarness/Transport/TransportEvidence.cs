namespace VerificationHarness.Transport;

public sealed class TransportWireFrameEvidence
{
    public string Direction { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public long Sequence { get; set; }
    public string WireSha256 { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
}

public sealed class TransportNodeResult
{
    public string EventKind { get; set; } = "node-result";
    public string Role { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int HighestGeneration { get; set; }
    public bool CleanReconnectObserved { get; set; }
    public string? RejectionCode { get; set; }
    public TransportStateSnapshot? LocalState { get; set; }
    public string? LocalDigest { get; set; }
    public Dictionary<string, string> ObservedDigests { get; set; } = new(StringComparer.Ordinal);
    public List<TransportWireFrameEvidence> WireFrames { get; set; } = new();
    public bool DeliveryDomainObserved { get; set; }
    public bool DeliveryDomainValid { get; set; } = true;
    public string RuntimeArtifactSetDigest { get; set; } = string.Empty;
    public ProcessRuntimeIdentity RuntimeIdentity { get; set; } = new();
}

public sealed class TransportProcessEvidence
{
    public string Role { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public int ExitCode { get; set; }
    public bool Killed { get; set; }
    public int StandardErrorBytes { get; set; }
    public string StandardErrorSha256 { get; set; } = string.Empty;
}

public sealed class ProcessPeerTopologyEvidence
{
    public string Transport { get; set; } = "LiteNetLib";
    public string TransportVersion { get; set; } = string.Empty;
    public string TransportAssemblyVersion { get; set; } = string.Empty;
    public string Address { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public int ServerCount { get; set; } = 1;
    public int ClientCount { get; set; } = 2;
    public int ProcessCount { get; set; } = 3;
    public bool ProcessIsolated { get; set; } = true;
    public int ProtocolVersion { get; set; } = TransportCodec.CurrentProtocolVersion;
    public string Serializer { get; set; } = "Common.Serialization.ProtoBufSerializer";
    public string TypeMapper { get; set; } = "Common.Serialization.SerializableTypeMapper";
}

public sealed class ProcessPeerDigestEvidence
{
    public string Algorithm { get; set; } = "sha256";
    public bool Converged { get; set; }
    public Dictionary<string, string> ByInstance { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ProcessPeerEvidence
{
    public string SchemaVersion { get; set; } = "process-peer.evidence.v1";
    public string Profile { get; set; } = "process-peer";
    public string Tier { get; set; } = "process-peer";
    public string EvidenceProfile { get; set; } = "functional";
    public string Head { get; set; } = string.Empty;
    public string Tree { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Verdict { get; set; } = "failed";
    public string Seed { get; set; } = string.Empty;
    public string ArtifactManifestSha256 { get; set; } = string.Empty;
    public ProcessRuntimeIdentity ManifestRuntimeIdentity { get; set; } = new();
    public ProcessRuntimeIdentity ControllerRuntimeIdentity { get; set; } = new();
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public ProcessPeerTopologyEvidence Topology { get; set; } = new();
    public SortedDictionary<string, bool> RequiredChecks { get; set; } = new(StringComparer.Ordinal);
    public ProcessPeerDigestEvidence Digest { get; set; } = new();
    public string ReplayIdentity { get; set; } = string.Empty;
    public List<string> WireHashes { get; set; } = new();
    public List<string> PayloadHashes { get; set; } = new();
    public SortedDictionary<string, string> ArtifactHashes { get; set; } = new(StringComparer.Ordinal);
    public List<TransportProcessEvidence> Processes { get; set; } = new();
    public List<TransportNodeResult> Nodes { get; set; } = new();
    public List<string> Failures { get; set; } = new();
}

public sealed class TransportReadyEvent
{
    public string EventKind { get; set; } = "ready";
    public string Role { get; set; } = "server";
    public int ProcessId { get; set; }
    public int Port { get; set; }
}
