using System.Text.Json;

namespace VerificationHarness.PeerHost;

public sealed class PeerHostSnapshotFields
{
    public string InstanceId { get; }
    public long Sequence { get; }
    public string LifecycleState { get; }
    public IReadOnlyDictionary<string, long> Counters { get; }
    public IReadOnlyDictionary<string, JsonElement> State { get; }

    public PeerHostSnapshotFields(
        string instanceId,
        long sequence,
        string lifecycleState,
        IReadOnlyDictionary<string, long> counters,
        IReadOnlyDictionary<string, JsonElement> state)
    {
        InstanceId = instanceId;
        Sequence = sequence;
        LifecycleState = lifecycleState;
        Counters = counters;
        State = state;
    }
}

public sealed class PeerHostSnapshotResult
{
    public string Algorithm { get; }
    public string Digest { get; }
    public PeerHostSnapshotFields Fields { get; }

    public PeerHostSnapshotResult(string digest, PeerHostSnapshotFields fields)
    {
        Algorithm = "sha256";
        Digest = digest;
        Fields = fields;
    }
}
