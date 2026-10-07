#if DEBUG
using Common.Messaging;
using Coop.Core.Server.Services.Save.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Coop.Core.Server.Services.Save;

/// <summary>
/// Records the typed session-write outcomes of one armed save name, so a live test can check the co-op sidecar
/// result of the save it started. DEBUG only; it never starts a save or writes campaign or session state.
/// </summary>
public interface ICoopSessionWriteObserver : IDisposable
{
    SessionWriteObservation Arm(string attemptId, string saveName);

    SessionWriteObservation Read(string attemptId);

    SessionWriteObservation Release(string attemptId);
}

public sealed class CoopSessionWriteObserver : ICoopSessionWriteObserver
{
    private const int MaxIgnoredNames = 16;
    private static readonly Regex AttemptPattern = new Regex("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant);
    private static readonly Regex SaveNamePattern = new Regex("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);

    private readonly IMessageBroker messageBroker;
    private readonly Func<DateTime> utcNow;
    private readonly object sync = new object();
    private Attempt current;
    private int unarmedCount;
    private long nextSequence;
    private bool disposed;

    public CoopSessionWriteObserver(IMessageBroker messageBroker)
        : this(messageBroker, () => DateTime.UtcNow)
    {
    }

    internal CoopSessionWriteObserver(IMessageBroker messageBroker, Func<DateTime> utcNow)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (utcNow == null) throw new ArgumentNullException(nameof(utcNow));

        this.messageBroker = messageBroker;
        this.utcNow = utcNow;
        messageBroker.Subscribe<CoopSessionWritten>(Handle_CoopSessionWritten);
    }

    public SessionWriteObservation Arm(string attemptId, string saveName)
    {
        lock (sync)
        {
            if (disposed) return Refused("The observer is disposed.", attemptId, saveName);
            if (attemptId == null || !AttemptPattern.IsMatch(attemptId))
                return Refused("The attempt id must be 1 to 128 letters, digits, dots, underscores or hyphens.", attemptId, saveName);
            if (saveName == null || !SaveNamePattern.IsMatch(saveName))
                return Refused("The save name must be 1 to 64 letters, digits, dots, underscores or hyphens.", attemptId, saveName);
            // A pending or unread observation is never discarded by a new arm.
            if (current != null)
                return Refused($"Attempt '{current.AttemptId}' is still armed; read and release it first.", attemptId, saveName);

            current = new Attempt(attemptId, saveName, utcNow());
            return Snapshot(current, "armed", null);
        }
    }

    public SessionWriteObservation Read(string attemptId)
    {
        lock (sync)
        {
            if (disposed) return Refused("The observer is disposed.", attemptId, null);
            if (current == null) return Refused("No observation is armed.", attemptId, null);
            if (!string.Equals(current.AttemptId, attemptId, StringComparison.Ordinal))
                return Refused($"Attempt '{attemptId}' is not the armed attempt.", attemptId, null);

            current.ReadCount++;
            current.ReadOutcomeCount = current.Outcomes.Count;
            return Snapshot(current, "observed", null);
        }
    }

    public SessionWriteObservation Release(string attemptId)
    {
        lock (sync)
        {
            if (disposed) return Refused("The observer is disposed.", attemptId, null);
            if (current == null) return Refused("No observation is armed.", attemptId, null);
            if (!string.Equals(current.AttemptId, attemptId, StringComparison.Ordinal))
                return Refused($"Attempt '{attemptId}' is not the armed attempt.", attemptId, null);
            // Only the exact state the caller has read can be released, so a late outcome is never dropped unseen.
            if (current.ReadCount == 0 || current.ReadOutcomeCount != current.Outcomes.Count)
                return Refused("Read the current observation before releasing it.", attemptId, null);

            current.ReleasedUtc = utcNow();
            var released = Snapshot(current, "released", null);
            current = null;
            return released;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
        }

        messageBroker.Unsubscribe<CoopSessionWritten>(Handle_CoopSessionWritten);
    }

    private void Handle_CoopSessionWritten(MessagePayload<CoopSessionWritten> payload)
    {
        lock (sync)
        {
            if (disposed) return;
            if (current == null)
            {
                unarmedCount++;
                return;
            }

            string saveName = payload.What.SaveName;
            if (!string.Equals(saveName, current.SaveName, StringComparison.Ordinal))
            {
                current.IgnoredCount++;
                if (current.IgnoredSaveNames.Count < MaxIgnoredNames) current.IgnoredSaveNames.Add(saveName ?? string.Empty);
                return;
            }

            current.Outcomes.Add(new SessionWriteOutcome(++nextSequence, payload.What.Success, utcNow()));
        }
    }

    private SessionWriteObservation Refused(string reason, string attemptId, string saveName) =>
        new SessionWriteObservation("refused", reason, "none", attemptId, saveName, null, Array.Empty<SessionWriteOutcome>(),
            Array.Empty<string>(), 0, unarmedCount, 0, null);

    private SessionWriteObservation Snapshot(Attempt attempt, string status, string reason) =>
        new SessionWriteObservation(status, reason, Classify(attempt.Outcomes), attempt.AttemptId, attempt.SaveName, attempt.ArmedUtc,
            attempt.Outcomes.ToArray(), attempt.IgnoredSaveNames.ToArray(), attempt.IgnoredCount, unarmedCount, attempt.ReadCount,
            attempt.ReleasedUtc);

    // A repeated same-name outcome is ambiguous, never a success.
    private static string Classify(List<SessionWriteOutcome> outcomes)
    {
        if (outcomes.Count == 0) return "pending";
        if (outcomes.Count > 1) return "ambiguous";
        return outcomes[0].Success ? "succeeded" : "failed";
    }

    private sealed class Attempt
    {
        public Attempt(string attemptId, string saveName, DateTime armedUtc)
        {
            AttemptId = attemptId;
            SaveName = saveName;
            ArmedUtc = armedUtc;
        }

        public string AttemptId { get; }
        public string SaveName { get; }
        public DateTime ArmedUtc { get; }
        public List<SessionWriteOutcome> Outcomes { get; } = new List<SessionWriteOutcome>();
        public List<string> IgnoredSaveNames { get; } = new List<string>();
        public int IgnoredCount { get; set; }
        public int ReadCount { get; set; }
        public int ReadOutcomeCount { get; set; }
        public DateTime? ReleasedUtc { get; set; }
    }
}

/// <summary>An immutable read of the armed session-write observation.</summary>
public sealed class SessionWriteObservation
{
    public SessionWriteObservation(string status, string reason, string result, string attemptId, string saveName, DateTime? armedUtc,
        IReadOnlyList<SessionWriteOutcome> outcomes, IReadOnlyList<string> ignoredSaveNames, int ignoredCount, int unarmedCount,
        int readCount, DateTime? releasedUtc)
    {
        Status = status;
        Reason = reason;
        Result = result;
        AttemptId = attemptId;
        SaveName = saveName;
        ArmedUtc = armedUtc;
        Outcomes = outcomes;
        IgnoredSaveNames = ignoredSaveNames;
        IgnoredCount = ignoredCount;
        UnarmedCount = unarmedCount;
        ReadCount = readCount;
        ReleasedUtc = releasedUtc;
    }

    /// <summary>armed, observed, released or refused.</summary>
    public string Status { get; }
    public string Reason { get; }
    /// <summary>pending, succeeded, failed, ambiguous, or none for a refusal.</summary>
    public string Result { get; }
    public string AttemptId { get; }
    public string SaveName { get; }
    public DateTime? ArmedUtc { get; }
    public IReadOnlyList<SessionWriteOutcome> Outcomes { get; }
    public IReadOnlyList<string> IgnoredSaveNames { get; }
    public int IgnoredCount { get; }
    public int UnarmedCount { get; }
    public int ReadCount { get; }
    public DateTime? ReleasedUtc { get; }
}

public sealed class SessionWriteOutcome
{
    public SessionWriteOutcome(long sequence, bool success, DateTime utc)
    {
        Sequence = sequence;
        Success = success;
        Utc = utc;
    }

    public long Sequence { get; }
    public bool Success { get; }
    public DateTime Utc { get; }
}
#endif
