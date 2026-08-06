#if DEBUG
using Missions.Agents.Packets;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Missions.Diagnostics;

internal static class ClientReplicationDiagnostics
{
    private const int MaximumTrackedSamples = 12000;
    private static readonly object Gate = new object();
    private static readonly Queue<int> ExpectedSamples = new Queue<int>();

    private static bool enabled;
    private static long acceptedSamples;
    private static long processedSamples;
    private static long failedSamples;
    private static long orderMismatches;
    private static long validationRejections;
    private static int injectedEntryFailures;
    private static int invalidProbeFingerprint;
    private static bool invalidProbeArmed;
    private static long invalidProbeRejections;
    private static long invalidProbeNativeApplications;
    private static long isolationFailures;
    private static long isolationRecoveries;
    private static bool awaitingIsolationRecovery;

    public static bool Enabled
    {
        get
        {
            lock (Gate) return enabled;
        }
    }

    public static void Start()
    {
        lock (Gate)
        {
            enabled = true;
            acceptedSamples = 0;
            processedSamples = 0;
            failedSamples = 0;
            orderMismatches = 0;
            validationRejections = 0;
            injectedEntryFailures = 0;
            invalidProbeFingerprint = 0;
            invalidProbeArmed = false;
            invalidProbeRejections = 0;
            invalidProbeNativeApplications = 0;
            isolationFailures = 0;
            isolationRecoveries = 0;
            awaitingIsolationRecovery = false;
            ExpectedSamples.Clear();
        }
    }

    public static string Snapshot(bool stop)
    {
        lock (Gate)
        {
            string result = string.Format(
                CultureInfo.InvariantCulture,
                "enabled={0} accepted={1} processed={2} failed={3} pendingOrder={4} " +
                "orderMismatches={5} rejected={6} invalidRejected={7} " +
                "invalidNative={8} isolationFailures={9} isolationRecoveries={10}",
                enabled,
                acceptedSamples,
                processedSamples,
                failedSamples,
                ExpectedSamples.Count,
                orderMismatches,
                validationRejections,
                invalidProbeRejections,
                invalidProbeNativeApplications,
                isolationFailures,
                isolationRecoveries);
            if (stop) enabled = false;
            return result;
        }
    }

    public static void RecordAccepted(MovementPacket packet, int index) =>
        RecordAccepted(Fingerprint(packet, index));

    public static void RecordAccepted(MountMovementPacket packet, int index) =>
        RecordAccepted(Fingerprint(packet, index));

    public static void RecordProcessed(MovementPacket packet, int index) =>
        RecordProcessed(Fingerprint(packet, index), failed: false);

    public static void RecordProcessed(MountMovementPacket packet, int index) =>
        RecordProcessed(Fingerprint(packet, index), failed: false);

    public static void RecordProcessed(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentData data) =>
        RecordProcessed(
            Fingerprint(scope, compactId, canonicalId, usesCompactId, data),
            failed: false);

    public static void RecordProcessed(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentMountData data) =>
        RecordProcessed(
            Fingerprint(scope, compactId, canonicalId, usesCompactId, data),
            failed: false);

    public static void RecordFailed(MovementPacket packet, int index) =>
        RecordProcessed(Fingerprint(packet, index), failed: true);

    public static void RecordFailed(MountMovementPacket packet, int index) =>
        RecordProcessed(Fingerprint(packet, index), failed: true);

    public static void RecordFailed(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentData data) =>
        RecordProcessed(
            Fingerprint(scope, compactId, canonicalId, usesCompactId, data),
            failed: true);

    public static void RecordFailed(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentMountData data) =>
        RecordProcessed(
            Fingerprint(scope, compactId, canonicalId, usesCompactId, data),
            failed: true);

    public static void RecordValidationRejection()
    {
        lock (Gate)
        {
            if (enabled) validationRejections++;
        }
    }

    public static void RecordValidationRejection(MovementPacket packet)
    {
        lock (Gate)
        {
            if (!enabled) return;
            validationRejections++;
            if (invalidProbeArmed && Fingerprint(packet, 0) == invalidProbeFingerprint)
            {
                invalidProbeRejections++;
                invalidProbeArmed = false;
            }
        }
    }

    public static void ArmInvalidProbe(MovementPacket packet)
    {
        lock (Gate)
        {
            invalidProbeFingerprint = Fingerprint(packet, 0);
            invalidProbeArmed = true;
        }
    }

    public static void RecordNativeApplication(MovementPacket packet)
    {
        lock (Gate)
        {
            if (enabled && invalidProbeArmed &&
                Fingerprint(packet, 0) == invalidProbeFingerprint)
            {
                invalidProbeNativeApplications++;
                invalidProbeArmed = false;
            }
        }
    }

    public static void RecordNativeApplication(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentData data)
    {
        lock (Gate)
        {
            if (enabled && invalidProbeArmed &&
                Fingerprint(scope, compactId, canonicalId, usesCompactId, data) ==
                    invalidProbeFingerprint)
            {
                invalidProbeNativeApplications++;
                invalidProbeArmed = false;
            }
        }
    }

    public static void ArmSingleEntryFailure()
    {
        lock (Gate)
        {
            injectedEntryFailures = 1;
        }
    }

    public static bool ConsumeInjectedEntryFailure()
    {
        lock (Gate)
        {
            if (injectedEntryFailures == 0) return false;
            injectedEntryFailures--;
            isolationFailures++;
            awaitingIsolationRecovery = true;
            return true;
        }
    }

    private static void RecordAccepted(int fingerprint)
    {
        lock (Gate)
        {
            if (!enabled) return;
            acceptedSamples++;
            if (ExpectedSamples.Count >= MaximumTrackedSamples)
            {
                ExpectedSamples.Dequeue();
                orderMismatches++;
            }
            ExpectedSamples.Enqueue(fingerprint);
        }
    }

    private static void RecordProcessed(int fingerprint, bool failed)
    {
        lock (Gate)
        {
            if (!enabled) return;
            if (ExpectedSamples.Count == 0 || ExpectedSamples.Dequeue() != fingerprint)
                orderMismatches++;
            if (failed)
                failedSamples++;
            else
            {
                processedSamples++;
                if (awaitingIsolationRecovery)
                {
                    isolationRecoveries++;
                    awaitingIsolationRecovery = false;
                }
            }
        }
    }

    private static int Fingerprint(MovementPacket packet, int index)
    {
        AgentData data = packet.Agents[index];
        int hash = StartFingerprint(packet.IdentityScopeId, packet.AgentIds, packet.AgentGuids, index);
        hash = Add(hash, data.Position.X);
        hash = Add(hash, data.Position.Y);
        hash = Add(hash, data.Position.Z);
        hash = Add(hash, data.LookDirection.X);
        hash = Add(hash, data.LookDirection.Y);
        hash = Add(hash, data.LookDirection.Z);
        hash = Add(hash, data.MovementDirection.X);
        hash = Add(hash, data.MovementDirection.Y);
        hash = Add(hash, data.InputVector.X);
        hash = Add(hash, data.InputVector.Y);
        hash = Add(hash, data.Speed);
        hash = Add(hash, data.MovementFlag);
        if (data.MountData != null)
            hash = AddMount(hash, data.MountData);
        return hash;
    }

    private static int Fingerprint(MountMovementPacket packet, int index)
    {
        int hash = StartFingerprint(packet.IdentityScopeId, packet.MountIds, packet.MountGuids, index);
        return AddMount(hash, packet.Mounts[index]);
    }

    private static int Fingerprint(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentData data)
    {
        int hash = StartFingerprint(scope, compactId, canonicalId, usesCompactId);
        hash = Add(hash, data.Position.X);
        hash = Add(hash, data.Position.Y);
        hash = Add(hash, data.Position.Z);
        hash = Add(hash, data.LookDirection.X);
        hash = Add(hash, data.LookDirection.Y);
        hash = Add(hash, data.LookDirection.Z);
        hash = Add(hash, data.MovementDirection.X);
        hash = Add(hash, data.MovementDirection.Y);
        hash = Add(hash, data.InputVector.X);
        hash = Add(hash, data.InputVector.Y);
        hash = Add(hash, data.Speed);
        hash = Add(hash, data.MovementFlag);
        if (data.MountData != null)
            hash = AddMount(hash, data.MountData);
        return hash;
    }

    private static int Fingerprint(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentMountData data)
    {
        int hash = StartFingerprint(scope, compactId, canonicalId, usesCompactId);
        return AddMount(hash, data);
    }

    private static int StartFingerprint(
        string scope,
        ushort[] compactIds,
        Guid[] canonicalIds,
        int index)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (scope?.GetHashCode() ?? 0);
            hash = (hash * 31) + (compactIds != null
                ? compactIds[index].GetHashCode()
                : canonicalIds[index].GetHashCode());
            return hash;
        }
    }

    private static int StartFingerprint(
        string scope,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (scope?.GetHashCode() ?? 0);
            hash = (hash * 31) + (usesCompactId
                ? compactId.GetHashCode()
                : canonicalId.GetHashCode());
            return hash;
        }
    }

    private static int AddMount(int hash, AgentMountData data)
    {
        hash = Add(hash, data.MountPosition.X);
        hash = Add(hash, data.MountPosition.Y);
        hash = Add(hash, data.MountPosition.Z);
        hash = Add(hash, data.MountLookDirection.X);
        hash = Add(hash, data.MountLookDirection.Y);
        hash = Add(hash, data.MountLookDirection.Z);
        hash = Add(hash, data.MountMovementDirection.X);
        hash = Add(hash, data.MountMovementDirection.Y);
        hash = Add(hash, data.MountInputVector.X);
        hash = Add(hash, data.MountInputVector.Y);
        hash = Add(hash, data.MountSpeed);
        return Add(hash, data.MountMovementFlag);
    }

    private static int Add(int hash, float value) =>
        Add(hash, value.GetHashCode());

    private static int Add(int hash, uint value) =>
        Add(hash, unchecked((int)value));

    private static int Add(int hash, int value)
    {
        unchecked
        {
            return (hash * 31) + value;
        }
    }
}
#endif
