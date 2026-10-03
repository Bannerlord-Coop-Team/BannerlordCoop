#if DEBUG
using System;
using NavalDLC.Missions.Objects;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal readonly PulsePhysicsObservation pulsePhysicsObservation = new PulsePhysicsObservation();

    // Bounded own-hull rowing summary for one axes pulse; parallel physics samples and game-thread reads share one lock.
    internal sealed class PulsePhysicsObservation
    {
        private readonly object gate = new object();
        private MissionShip ship;
        private Guid operation;
        private int generation;
        private bool open;
        private int samples;
        private float sampledSeconds;
        private int thrustSamples;
        private float rowerThrustMax;
        private int usedOarsMin;
        private int usedOarsMax;
        private int totalOars;
        private float phaseLast;
        private float phaseRateLast;
        private float phaseRateMax;
        private float oarForwardForceMax;
        private float oarForwardImpulse;
        private float bodyForwardSpeedFirst;
        private float bodyForwardSpeedLast;
        private float bodyForwardSpeedMax;
        private int anchoredSamples;
        private float submergedFactorMin;
        private float submergedFactorLast;

        // Game thread: starts a fresh window, so samples issued for an earlier window can no longer match.
        internal void Begin(Guid operationId, MissionShip ownShip)
        {
            lock (gate)
            {
                ship = ownShip;
                operation = operationId;
                generation++;
                open = ownShip != null;
                samples = thrustSamples = usedOarsMin = usedOarsMax = totalOars = anchoredSamples = 0;
                sampledSeconds = rowerThrustMax = phaseLast = phaseRateLast = phaseRateMax = 0;
                oarForwardForceMax = oarForwardImpulse = bodyForwardSpeedFirst = bodyForwardSpeedLast = bodyForwardSpeedMax = 0;
                submergedFactorMin = submergedFactorLast = 0;
            }
        }

        // Game thread: freezes the window when the pulse ends and drops the hull reference.
        internal void Close()
        {
            lock (gate)
            {
                open = false;
                ship = null;
            }
        }

        // Physics thread, before vanilla rowing runs: the window id for the open own hull, otherwise -1.
        internal int Generation(MissionShip owner)
        {
            lock (gate) return open && ReferenceEquals(owner, ship) ? generation : -1;
        }

        // Physics thread, after vanilla rowing runs: adds one tick only if its window is still the open one.
        internal void Record(MissionShip owner, int sampleGeneration, float fixedDt, float rowerThrust, int usedOars, int oars,
            float phase, float phaseRate, float oarForwardForce, float bodyForwardSpeed, bool anchored, float submergedFactor)
        {
            lock (gate)
            {
                if (!open || sampleGeneration != generation || !ReferenceEquals(owner, ship)) return;
                if (samples == 0)
                {
                    rowerThrustMax = rowerThrust;
                    usedOarsMin = usedOarsMax = usedOars;
                    phaseRateMax = phaseRate;
                    oarForwardForceMax = oarForwardForce;
                    bodyForwardSpeedFirst = bodyForwardSpeedMax = bodyForwardSpeed;
                    submergedFactorMin = submergedFactor;
                }
                samples++;
                sampledSeconds += fixedDt;
                if (rowerThrust != 0) thrustSamples++;
                rowerThrustMax = Math.Max(rowerThrustMax, rowerThrust);
                usedOarsMin = Math.Min(usedOarsMin, usedOars);
                usedOarsMax = Math.Max(usedOarsMax, usedOars);
                totalOars = oars;
                phaseLast = phase;
                phaseRateLast = phaseRate;
                phaseRateMax = Math.Max(phaseRateMax, phaseRate);
                oarForwardForceMax = Math.Max(oarForwardForceMax, oarForwardForce);
                oarForwardImpulse += oarForwardForce * fixedDt;
                bodyForwardSpeedLast = bodyForwardSpeed;
                bodyForwardSpeedMax = Math.Max(bodyForwardSpeedMax, bodyForwardSpeed);
                if (anchored) anchoredSamples++;
                submergedFactorMin = Math.Min(submergedFactorMin, submergedFactor);
                submergedFactorLast = submergedFactor;
            }
        }

        // Game thread: one coherent copy of primitives; non-finite values export as null, never as zero.
        internal object Snapshot()
        {
            lock (gate)
            {
                if (operation == Guid.Empty) return new { unavailable = "no_operation" };
                if (samples == 0) return new { operationId = operation, open, samples, unavailable = "no_own_hull_sample" };
                return new
                {
                    operationId = operation, open, samples, sampledSeconds = Finite(sampledSeconds),
                    thrustSamples, rowerThrustMax = Finite(rowerThrustMax), usedOarsMin, usedOarsMax, totalOars,
                    phaseLast = Finite(phaseLast), phaseRateLast = Finite(phaseRateLast), phaseRateMax = Finite(phaseRateMax),
                    oarForwardForceMax = Finite(oarForwardForceMax), oarForwardImpulse = Finite(oarForwardImpulse),
                    bodyForwardSpeedFirst = Finite(bodyForwardSpeedFirst), bodyForwardSpeedLast = Finite(bodyForwardSpeedLast),
                    bodyForwardSpeedMax = Finite(bodyForwardSpeedMax), anchoredSamples,
                    submergedFactorMin = Finite(submergedFactorMin), submergedFactorLast = Finite(submergedFactorLast),
                    basis = "ShipActuators.FixedUpdateRowers postfix on the own hull from pulse accept until neutral or cancel; "
                        + "oar force is left+right ShipForce dot flattened body forward (N); speed is world velocity dot body forward at tick start (m/s)"
                };
            }
        }

        // Keeps the exported DTO finite so serialization never carries NaN or infinity.
        private static float? Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? null : value;
    }
}
#endif
