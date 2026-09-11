#if DEBUG
using System;
using System.Linq;
using TaleWorlds.Library;
using Missions.Messages;

namespace Missions.Battles;

public sealed partial class NavalLabController
{
    private const float HullInterpolationSeconds = 0.05f;
    private MatrixFrame[] hullStartFrames;
    private MatrixFrame[] hullTargetFrames;
    private MatrixFrame[] hullWrittenFrames;
    private MatrixFrame[] hullObservedFrames;
    private float hullElapsed;
    private long hullTargetSequence;
    private long hullTargetSourceCallback;
    private long hullTargetAcceptedCallback;
    private long hullTargetAcceptedUtcTicks;
    private long hullApplicationOrdinal;
    private long hullApplicationSequence;
    private long hullApplicationSourceCallback;
    private long hullApplicationCallback;
    private long hullApplicationUtcTicks;
    private float hullApplicationAlpha;

    private bool CanWriteFollowerHull => IsTwoClientNative && !disposed && released && factoryHydrated
        && FactoryAssignmentValid && !session.IsLocalHost && adapter.Blocker == null && NativeAgentAuthoritiesValid
        && Mission != null && Mission == TaleWorlds.MountAndBlade.Mission.Current;

    private void ClearHullTargets()
    {
        hullStartFrames = null;
        hullTargetFrames = null;
        hullElapsed = 0;
    }

    private bool AcceptHullTarget(NetworkNavalLabFrames message, MatrixFrame[] frames)
    {
        if (!CanWriteFollowerHull) { ClearHullTargets(); return false; }
        hullStartFrames = hullWrittenFrames;
        hullTargetFrames = frames;
        hullTargetSequence = message.Sequence;
        hullTargetSourceCallback = message.SourceCallback;
        hullTargetAcceptedCallback = callback;
        hullTargetAcceptedUtcTicks = DateTime.UtcNow.Ticks;
        hullElapsed = 0;
        // Hydration has no preceding authoritative pose to blend from.
        return hullStartFrames != null || WriteFollowerHull(frames, 1);
    }

    private void TickFollowerHull(float dt)
    {
        if (!IsTwoClientNative) return;
        if (!CanWriteFollowerHull) { ClearHullTargets(); return; }
        if (hullTargetFrames == null || hullElapsed >= HullInterpolationSeconds || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
        hullElapsed = Math.Min(HullInterpolationSeconds, hullElapsed + Math.Min(dt, HullInterpolationSeconds));
        float alpha = Math.Min(1, Math.Max(0, hullElapsed / HullInterpolationSeconds));
        var frames = alpha >= 1 ? hullTargetFrames : new[]
        {
            MatrixFrame.Lerp(hullStartFrames[0], hullTargetFrames[0], alpha),
            MatrixFrame.Lerp(hullStartFrames[1], hullTargetFrames[1], alpha)
        };
        if (!WriteFollowerHull(frames, alpha)) throw new InvalidOperationException("native.hull_frame_apply_refused");
    }

    private bool WriteFollowerHull(MatrixFrame[] frames, float alpha)
    {
        if (!CanWriteFollowerHull) { ClearHullTargets(); return false; }
        if (!FiniteHullFrames(frames)) throw new InvalidOperationException("native.hull_interpolation_nonfinite");
        if (!adapter.ApplyFrames(frames)) return false;
        if (!CanWriteFollowerHull) { ClearHullTargets(); return false; }
        var observed = adapter.ReadFrames();
        if (!FiniteHullFrames(observed)) throw new InvalidOperationException("native.hull_readback_nonfinite");
        hullWrittenFrames = frames;
        hullObservedFrames = observed;
        hullApplicationOrdinal++;
        hullApplicationSequence = hullTargetSequence;
        hullApplicationSourceCallback = hullTargetSourceCallback;
        hullApplicationCallback = callback;
        hullApplicationUtcTicks = DateTime.UtcNow.Ticks;
        hullApplicationAlpha = alpha;
        if (alpha >= 1)
        {
            lastApplied = hullTargetSequence;
            lastAppliedFrameSourceCallback = hullTargetSourceCallback;
            lastAppliedFrameUtcTicks = hullApplicationUtcTicks;
            if (pendingSample?.Sequence == hullTargetSequence) pendingAppliedCallback = callback;
            hullElapsed = HullInterpolationSeconds;
            hullStartFrames = null;
        }
        return true;
    }

    private static bool FiniteHullFrames(MatrixFrame[] frames) => frames != null && frames.Length == 2
        && HullFrameScalars(frames).All(value => !float.IsNaN(value) && !float.IsInfinity(value));

    private static float[] HullFrameScalars(MatrixFrame[] frames) => frames?.SelectMany(frame => new[]
    {
        frame.rotation.s.x, frame.rotation.s.y, frame.rotation.s.z,
        frame.rotation.f.x, frame.rotation.f.y, frame.rotation.f.z,
        frame.rotation.u.x, frame.rotation.u.y, frame.rotation.u.z,
        frame.origin.x, frame.origin.y, frame.origin.z
    }).ToArray();

    private object HullInterpolationStatus() => new
    {
        windowSeconds = HullInterpolationSeconds, isTeleportation = false,
        pending = hullTargetFrames != null && hullElapsed < HullInterpolationSeconds,
        acceptedTargetSequence = hullTargetSequence, acceptedSourceCallback = hullTargetSourceCallback,
        acceptedLocalCallback = hullTargetAcceptedCallback, acceptedUtcTicks = hullTargetAcceptedUtcTicks,
        targetFrames = HullFrameScalars(hullTargetFrames),
        applicationOrdinal = hullApplicationOrdinal, applicationTargetSequence = hullApplicationSequence,
        applicationSourceCallback = hullApplicationSourceCallback, applicationLocalCallback = hullApplicationCallback,
        applicationUtcTicks = hullApplicationUtcTicks, applicationAlpha = hullApplicationAlpha,
        applicationCompletedTarget = hullApplicationOrdinal > 0 && hullApplicationAlpha >= 1,
        lastWrittenFrames = HullFrameScalars(hullWrittenFrames), lastNativeReadbackFrames = HullFrameScalars(hullObservedFrames)
    };
}
#endif
