#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

public interface INavalLabMeasurement
{
    Guid OperationId { get; }
    bool Active(double now);
    string Begin(Guid operationId, int epoch, double now);
    void Cancel(string reason);
    void Add(long sequence, long sourceCallback, long? receivedCallback, long? appliedCallback,
        long? observedCallback, float[] transmittedFrames, object native, string error);
    object Read(long afterSequence);
}

// A bounded observation window, not a recoverable snapshot or a native tick barrier.
public sealed class NavalLabMeasurement : INavalLabMeasurement
{
    public const int Capacity = 64;
    public const int PageSize = 8;
    private readonly Queue<Sample> samples = new Queue<Sample>();
    private readonly HashSet<Guid> operations = new HashSet<Guid>();
    private double deadline;
    private int epoch;
    private long overwritten;
    private string status = "unavailable:not_started";
    public Guid OperationId { get; private set; }

    public bool Active(double now)
    {
        if (status == "active" && now >= deadline) status = "expired";
        return status == "active";
    }

    public string Begin(Guid operationId, int epoch, double now)
    {
        if (operationId == Guid.Empty || epoch != 1 || double.IsNaN(now) || double.IsInfinity(now))
            return "rejected:invalid_probe_identity_or_time";
        if (operations.Contains(operationId)) return "duplicate:not_restarted";
        if (Active(now)) return "rejected:probe_active";
        if (operations.Count >= 8) return "rejected:probe_budget";
        operations.Add(operationId);
        OperationId = operationId;
        this.epoch = epoch;
        deadline = now + 30;
        samples.Clear();
        overwritten = 0;
        status = "active";
        return "applied";
    }

    public void Cancel(string reason)
    {
        if (status == "active") status = "cancelled:" + reason;
    }

    public void Add(long sequence, long sourceCallback, long? receivedCallback, long? appliedCallback,
        long? observedCallback, float[] transmittedFrames, object native, string error)
    {
        if (OperationId == Guid.Empty || sequence <= 0 || sourceCallback <= 0
            || transmittedFrames == null || transmittedFrames.Length != 24
            || transmittedFrames.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new ArgumentException("Invalid scalar sample identity or frames.");
        if (samples.Count == Capacity) { samples.Dequeue(); overwritten++; }
        samples.Enqueue(new Sample(sequence, sourceCallback, receivedCallback, appliedCallback,
            observedCallback, transmittedFrames, native, error));
    }

    public object Read(long afterSequence) => new
    {
        operationId = OperationId, epoch, status, capacity = Capacity, overwritten,
        clock = "process-local mission callback ordinals; not native fixed ticks or a barrier",
        samples = samples.Where(sample => sample.sequence > afterSequence).Take(PageSize).ToArray()
    };

    private sealed class Sample
    {
        public long sequence { get; }
        public long hostSampleCallback { get; }
        public long? receivedCallback { get; }
        public long? appliedCallback { get; }
        public long? observedCallback { get; }
        public float[] transmittedFrames { get; }
        public object native { get; }
        public string error { get; }
        public Sample(long sequence, long sourceCallback, long? receivedCallback, long? appliedCallback,
            long? observedCallback, float[] frames, object native, string error)
        {
            this.sequence = sequence;
            hostSampleCallback = sourceCallback;
            this.receivedCallback = receivedCallback;
            this.appliedCallback = appliedCallback;
            this.observedCallback = observedCallback;
            transmittedFrames = (float[])frames.Clone();
            this.native = native;
            this.error = error;
        }
    }
}
#endif
