#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipActuators;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private sealed class SailObservation
    {
        internal volatile SailCallback Value;
    }
    private sealed class SailCallback
    {
        public NetworkNavalLabSailPresentation State { get; }
        public long Sequence { get; }
        public float MorphKey { get; }
        public long UtcTicks { get; } = DateTime.UtcNow.Ticks;
        internal SailCallback(NetworkNavalLabSailPresentation state, long sequence, float morphKey)
        { State = state; Sequence = sequence; MorphKey = morphKey; }
    }
    private sealed class OarObservation
    {
        internal volatile OarCallback Value;
    }
    private sealed class OarCallback
    {
        public NetworkNavalLabOarPresentation State { get; }
        public long Sequence { get; }
        public float? ActionProgress { get; }
        public int? Action { get; }
        public long UtcTicks { get; } = DateTime.UtcNow.Ticks;
        public object CallsiteTrace { get; }
        internal OarCallback(NetworkNavalLabOarPresentation state, long sequence, float? actionProgress, int? action, object callsiteTrace)
        { State = state; Sequence = sequence; ActionProgress = actionProgress; Action = action; CallsiteTrace = callsiteTrace; }
    }
    private sealed class PresentationShip
    {
        internal readonly MissionShip Ship;
        internal readonly MissionSail[] Sails;
        internal readonly string[] SailKeys;
        internal readonly ShipOarMachine[] Machines;
        internal readonly string[] OarKeys;
        internal readonly OarObservation[] Observations;
        internal readonly SailObservation[] SailObservations;
        internal readonly Guid[] Combatants;
        internal readonly Agent[] Actors;
        internal PresentationShip(MissionShip ship, MissionSail[] sails, string[] sailKeys, ShipOarMachine[] machines, string[] oarKeys, Guid[] combatants, Agent[] actors)
        { Ship = ship; Sails = sails; SailKeys = sailKeys; Machines = machines; OarKeys = oarKeys; Combatants = combatants; Actors = actors;
            Observations = machines.Select(_ => new OarObservation()).ToArray();
            SailObservations = sails.Select(_ => new SailObservation()).ToArray(); }
    }

    private sealed class DisplayedPresentation
    {
        internal readonly NetworkNavalLabPresentation[] Ships;
        internal readonly long Sequence;
        internal DisplayedPresentation(NetworkNavalLabPresentation[] ships, long sequence) { Ships = ships; Sequence = sequence; }
    }

    private volatile PresentationShip[] presentationInventory;
    private volatile bool presentationCaptureEnabled;
    private volatile DisplayedPresentation displayedPresentation;
    private NetworkNavalLabPresentation[] presentationTarget;
    private NetworkNavalLabPresentation[] presentationStart;
    private NetworkNavalLabPresentation[] hostCapturedPresentation;
    private long presentationSequence, presentationSourceCallback, presentationCapturedSequence;
    private double presentationDeadline;
    private float presentationElapsed;
    private long sailVisualCallbacks, sailFoldCallbacks, oarVisualCallbacks;
    private long presentationCapturedUtcTicks;
    private long oarCallsiteOrdinal;
    private string presentationUnavailable = "not_initialized";
    private bool PresentationReady => IsTwoClientNative && Mission != null && Mission == Mission.Current
        && !factoryTerminal && !nativeTerminalHold && Blocker == null && CanUseNativeControls;

    private string SailKey(MissionSail sail, MissionShip ship)
    {
        var entity = sail.SailEntity.WeakEntity;
        var parts = new List<string>();
        while (entity.IsValid && entity != ship.GameEntity && parts.Count < 16)
        {
            var parent = entity.Parent;
            if (!parent.IsValid) throw new InvalidOperationException("presentation.sail_outside_hull");
            int index = parent.GetChildren().ToList().IndexOf(entity);
            if (index < 0) throw new InvalidOperationException("presentation.sail_child_missing");
            parts.Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + entity.Name);
            entity = parent;
        }
        if (!entity.IsValid || entity != ship.GameEntity) throw new InvalidOperationException("presentation.sail_outside_hull");
        parts.Reverse();
        return string.Join("/", parts);
    }

    private bool PreparePresentationInventory()
    {
        if (!PresentationReady) return false;
        presentationCaptureEnabled = true;
        if (presentationInventory != null) return true;
        var inventory = new PresentationShip[2];
        for (int slot = 0; slot < 2; slot++)
        {
            var ship = Ships[slot];
            var stations = StationInventory(slot).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            var sails = ship.Sails.ToArray();
            // This spike's fixture uses square sails; lateen has a separate yard/roll animation clock.
            if (sails.Length == 0 || sails.Length > 16 || stations.Length > 128
                || sails.Any(sail => sail?.SailObject == null || (int)sail.SailObject.Type != 0
                    || sail._sailVisual == null || !sail.SailEntity.WeakEntity.IsValid || !sail._sailVisual.SailYawRotationEntity.WeakEntity.IsValid
                    || new[] { sail._sailVisual._foldSailDuration, sail._sailVisual._unfoldSailDuration,
                        sail._sailVisual._foldFreeBoneResetDuration, sail._sailVisual._foldedSailTransitionDuration }
                        .Any(value => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > 120)))
            { presentationCaptureEnabled = false; presentationUnavailable = "unsupported_sail_inventory_or_lifetime:square_only"; return false; }
            var committed = appliedStations[slot];
            var combatants = stations.Select(pair => Array.IndexOf(committed.Keys, pair.Key)).Select(index =>
                index < 0 ? Guid.Empty : committed.Combatants[index]).ToArray();
            var actors = combatants.Select(id => id == Guid.Empty ? null : Agents[Array.IndexOf(manifest.Combatants, id)]).ToArray();
            inventory[slot] = new PresentationShip(ship, sails, sails.Select(sail => SailKey(sail, ship)).ToArray(),
                stations.Select(pair => pair.Value).ToArray(), stations.Select(pair => pair.Key).ToArray(), combatants, actors);
        }
        presentationInventory = inventory;
        presentationUnavailable = null;
        return true;
    }

    private NetworkNavalLabPresentation[] ObservePresentation()
    {
        if (!PresentationReady || presentationInventory == null) return null;
        var result = new NetworkNavalLabPresentation[2];
        for (int slot = 0; slot < 2; slot++)
        {
            var entry = presentationInventory[slot];
            if (!entry.Ship.GameEntity.IsValid || entry.Ship != Ships[slot]) return null;
            var sails = entry.SailObservations.Select(observation => observation.Value).Select(value =>
                value != null && value.UtcTicks > DateTime.UtcNow.AddSeconds(-1).Ticks ? value.State : null).ToArray();
            var oars = entry.Observations.Select(observation => observation.Value).Select(value =>
                value != null && value.UtcTicks > DateTime.UtcNow.AddSeconds(-1).Ticks ? value.State : null).ToArray();
            var sides = new[] { entry.Ship._actuators._leftOarsPhaseController, entry.Ship._actuators._rightOarsPhaseController }
                .SelectMany(side => new[] { side.Phase, side.VisualPhase, side.PhaseRate, side.CycleArcSizeMult, side.NeededRevolutionRate }).ToArray();
            result[slot] = new NetworkNavalLabPresentation(manifest.Ships[slot], sails, oars, sides);
        }
        return result.All(ship => ship.IsValid) ? result : null;
    }

    internal NetworkNavalLabPresentation[] CapturePresentation(long sequence)
    {
        if (!GameThread.Instance.IsGameThread || !factoryHost || !PreparePresentationInventory()) return null;
        hostCapturedPresentation = ObservePresentation();
        presentationCapturedSequence = sequence;
        presentationCapturedUtcTicks = DateTime.UtcNow.Ticks;
        presentationUnavailable = hostCapturedPresentation == null ? "native_capture_missing_stale_or_invalid" : null;
        return hostCapturedPresentation;
    }

    internal bool ValidatePresentation(NetworkNavalLabFrames frames)
    {
        if (frames.Presentation == null) return true;
        if (!GameThread.Instance.IsGameThread || factoryHost || frames.IncarnationId != manifest.IncarnationId
            || frames.Epoch != 1 || frames.Sequence <= presentationSequence || frames.Presentation.Length != 2
            || frames.Presentation.Any(value => value == null || !value.IsValid)
            || frames.SailDeadlineUtcTicks <= DateTime.UtcNow.Ticks || frames.SailDeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks
            || !PreparePresentationInventory()) return false;
        for (int slot = 0; slot < 2; slot++)
        {
            var value = frames.Presentation[slot];
            var inventory = presentationInventory[slot];
            if (value == null || !value.IsValid || value.ShipId != manifest.Ships[slot]
                || !value.Sails.Select(sail => sail.Key).SequenceEqual(inventory.SailKeys)
                || !value.Oars.Select(oar => oar.Key).SequenceEqual(inventory.OarKeys)
                || value.Sails.Any(sail => sail.Type != 0)) return false;
            for (int i = 0; i < value.Oars.Length; i++)
                if (value.Oars[i].Side != (int)inventory.Machines[i]._oar._sidePhaseData.Side) return false;
        }
        return true;
    }

    internal void AcceptPresentation(NetworkNavalLabFrames frames)
    {
        if (frames.Presentation == null || frames.Sequence <= presentationSequence || !PresentationReady
            || frames.IncarnationId != manifest.IncarnationId || frames.Epoch != 1
            || frames.SailDeadlineUtcTicks <= DateTime.UtcNow.Ticks) return;
        presentationStart = displayedPresentation?.Ships;
        if (presentationStart != null) presentationStart = RebaseSailTransitions(presentationStart, frames.Presentation);
        presentationTarget = frames.Presentation;
        presentationSequence = frames.Sequence;
        presentationSourceCallback = frames.SourceCallback;
        presentationDeadline = ControlNow + Math.Max(0, TimeSpan.FromTicks(frames.SailDeadlineUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds);
        presentationElapsed = 0;
        if (presentationStart == null)
        {
            displayedPresentation = new DisplayedPresentation(presentationTarget, presentationSequence);
            presentationElapsed = 0.05f;
        }
    }

    internal void TickPresentation(float dt)
    {
        if (!PresentationReady) { ClearPresentation(); return; }
        if (!PreparePresentationInventory()) return;
        if (factoryHost || presentationTarget == null || presentationElapsed >= 0.05f || ControlNow >= presentationDeadline
            || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
        presentationElapsed = Math.Min(0.05f, presentationElapsed + Math.Min(dt, 0.05f));
        float alpha = presentationElapsed / 0.05f;
        displayedPresentation = new DisplayedPresentation(presentationTarget.Select((ship, slot) => ship.BlendFrom(presentationStart[slot], alpha)).ToArray(), presentationSequence);
    }

    internal void ClearPresentation()
    {
        presentationCaptureEnabled = false;
        displayedPresentation = null;
        presentationTarget = presentationStart = null;
        presentationDeadline = 0;
    }

    internal NetworkNavalLabSailPresentation FindSailPresentation(SailVisual visual) => FindSailPresentation(visual, out _);

    internal NetworkNavalLabSailPresentation FindSailPresentation(SailVisual visual, out long sequence)
    {
        var shown = displayedPresentation;
        sequence = shown?.Sequence ?? 0;
        var inventory = presentationInventory;
        if (shown == null || inventory == null) return null;
        for (int slot = 0; slot < inventory.Length; slot++)
            for (int i = 0; i < inventory[slot].Sails.Length; i++)
                if (inventory[slot].Sails[i]._sailVisual == visual) return shown.Ships[slot].Sails[i];
        return null;
    }

    internal NetworkNavalLabOarPresentation FindOarPresentation(ShipOarMachine machine, out long sequence)
    {
        var shown = displayedPresentation;
        sequence = shown?.Sequence ?? 0;
        var inventory = presentationInventory;
        if (shown == null || inventory == null) return null;
        for (int slot = 0; slot < inventory.Length; slot++)
        {
            int index = Array.IndexOf(inventory[slot].Machines, machine);
            if (index >= 0) return shown.Ships[slot].Oars[index];
        }
        return null;
    }

    internal void RecordSailPresentation(SailVisual visual, long sequence)
    {
        var inventory = presentationInventory;
        if (!presentationCaptureEnabled || inventory == null) return;
        for (int slot = 0; slot < inventory.Length; slot++)
        {
            var entry = inventory[slot];
            for (int i = 0; i < entry.Sails.Length; i++)
            {
                var sail = entry.Sails[i];
                if (sail._sailVisual != visual) continue;
                if (!sail.SailEntity.WeakEntity.IsValid || !visual.SailYawRotationEntity.WeakEntity.IsValid) return;
                var animation = visual._ongoingAnimationData;
                float yaw = visual.SailYawRotationEntity.GetLocalFrame().rotation.GetEulerAngles().z;
                var value = new NetworkNavalLabSailPresentation(entry.SailKeys[i], (int)sail.SailObject.Type,
                    sail.TargetSailSetting, sail.Setting, yaw, visual.SailEnabled,
                    animation.FoldIsOngoing, animation.UnfoldIsOngoing, animation.CurrentProgress, animation.RealProgress);
                if (!value.IsValid || float.IsNaN(visual._lastMorphAnimKeySet) || float.IsInfinity(visual._lastMorphAnimKeySet)) return;
                entry.SailObservations[i].Value = new SailCallback(value, sequence, visual._lastMorphAnimKeySet);
                Interlocked.Increment(ref sailFoldCallbacks);
                return;
            }
        }
    }

    internal bool BeginOarCallsiteTrace(ShipOarMachine machine, out Guid combatant, out string controller, out long ordinal)
    {
        combatant = Guid.Empty; controller = null; ordinal = 0;
        var inventory = presentationInventory;
        if (!presentationCaptureEnabled || inventory == null || factoryTerminal || nativeTerminalHold || Blocker != null) return false;
        foreach (var entry in inventory)
        {
            int index = Array.IndexOf(entry.Machines, machine);
            if (index < 0) continue;
            var agent = entry.Actors[index];
            var point = machine.PilotStandingPoint;
            if (agent == null || agent.Pointer == UIntPtr.Zero || agent.Mission != Mission || !agent.IsActive()
                || !entry.Ship.GameEntity.IsValid || !machine.GameEntity.IsValid || point == null || !point.GameEntity.IsValid
                || machine.PilotAgent != agent || point.UserAgent != agent || agent.CurrentlyUsedGameObject != point
                || machine._lastPilotAgent != agent || !machine._isPilotSitting) return false;
            combatant = entry.Combatants[index];
            controller = agent.Controller.ToString();
            ordinal = Interlocked.Increment(ref oarCallsiteOrdinal);
            return true;
        }
        return false;
    }

    internal void RecordOarPresentation(ShipOarMachine machine, long sequence, object callsiteTrace,
        float? consumedPhase, float? consumedRate, bool? consumedRowing, float? consumedExtraction)
    {
        var inventory = presentationInventory;
        if (!presentationCaptureEnabled || inventory == null) return;
        for (int slot = 0; slot < inventory.Length; slot++)
        {
            var entry = inventory[slot];
            int index = Array.IndexOf(entry.Machines, machine);
            if (index < 0) continue;
            if (!machine.GameEntity.IsValid || !machine._oarEntity.WeakEntity.IsValid) return;
            var oar = machine._oar;
            var value = new NetworkNavalLabOarPresentation(entry.OarKeys[index], (int)oar._sidePhaseData.Side,
                consumedPhase ?? oar.VisualPhase, consumedExtraction ?? oar.Extraction,
                consumedRate ?? oar.NeededRevolutionRate, consumedRowing ?? oar.IsInRowingMotion(),
                NetworkNavalLabOarPresentation.FromFrame(machine._oarEntity.GetLocalFrame()));
            if (!value.IsValid) return;
            var agent = machine.PilotAgent;
            float? progress = agent?.GetCurrentActionProgress(0);
            if (progress.HasValue && (float.IsNaN(progress.Value) || float.IsInfinity(progress.Value))) progress = null;
            entry.Observations[index].Value = new OarCallback(value, sequence, progress, agent?.GetCurrentAction(0).Index, callsiteTrace);
            Interlocked.Increment(ref oarVisualCallbacks);
            return;
        }
    }

    private NetworkNavalLabPresentation[] RebaseSailTransitions(NetworkNavalLabPresentation[] start, NetworkNavalLabPresentation[] target)
    {
        return start.Select((ship, slot) => new NetworkNavalLabPresentation(ship.ShipId,
            ship.Sails.Select((sail, i) =>
            {
                var next = target[slot].Sails[i];
                if (sail.Folding == next.Folding && sail.Unfolding == next.Unfolding) return sail;
                var visual = presentationInventory[slot].Sails[i]._sailVisual;
                float reset = visual._foldFreeBoneResetDuration, transition = visual._foldedSailTransitionDuration;
                float fold = visual._foldSailDuration, unfold = visual._unfoldSailDuration;
                float progress = 0;
                // Native CancelAnimation maps the same displayed opening into the opposite clock.
                if (next.Folding && sail.Unfolding)
                    progress = sail.Progress < transition ? fold + reset + (transition - sail.Progress)
                        : sail.Progress < transition + unfold ? reset + (fold * (1 - ((sail.Progress - transition) / unfold)))
                        : (unfold + reset + transition - sail.Progress) * reset / transition;
                else if (next.Unfolding && sail.Folding)
                    progress = sail.Progress < reset ? unfold + reset + (transition - sail.Progress)
                        : sail.Progress < fold + reset ? transition + (unfold * (1 - ((sail.Progress - reset) / fold)))
                        : (fold + reset + transition - sail.Progress) * transition / reset;
                return new NetworkNavalLabSailPresentation(sail.Key, sail.Type, sail.Target, sail.Setting, sail.Yaw,
                    next.Enabled, next.Folding, next.Unfolding, Math.Max(0, progress), 0);
            }).ToArray(), ship.Oars, ship.Sides)).ToArray();
    }

    internal bool PresentSail(MissionSail sail)
    {
        var state = FindSailPresentation(sail._sailVisual);
        if (state == null) return false;
        var visual = sail._sailVisual;
        var frame = visual.SailYawRotationEntity.GetLocalFrame();
        frame.rotation = Mat3.Identity;
        frame.rotation.RotateAboutUp(state.Yaw);
        visual.SailYawRotationEntity.SetLocalFrame(ref frame, false);
        visual.SailEnabled = state.Enabled;
        Interlocked.Increment(ref sailVisualCallbacks);
        return true;
    }

    private bool IsPresentationSample(int slot, string key) => appliedStations.TryGetValue(slot, out var stations)
        && stations.Keys.Contains(key);

    private object[] PresentationSummary(NetworkNavalLabPresentation[] states) => states?.Select((ship, slot) => (object)new
    {
        ship.ShipId, ship.Sails, ship.Sides, oarCount = ship.Oars.Length,
        occupiedOars = ship.Oars.Where(oar => IsPresentationSample(slot, oar.Key)).Take(4).ToArray()
    }).ToArray();

    internal object InspectPresentationStatus()
    {
        if (!GameThread.Instance.IsGameThread) return new { unavailable = "not_game_thread" };
        bool ready = PresentationReady;
        var shown = displayedPresentation;
        return new
        {
            manifest.IncarnationId, epoch = 1, electedSimulator = factoryHost, ready, supportedSails = "square_only",
            unavailable = ready ? presentationUnavailable : "not_ready_or_terminal",
            capturedSequence = presentationCapturedSequence, capturedUtcTicks = presentationCapturedUtcTicks, captured = PresentationSummary(hostCapturedPresentation),
            acceptedSequence = presentationSequence, sourceCallback = presentationSourceCallback, accepted = PresentationSummary(presentationTarget),
            displayedSequence = shown?.Sequence, displayed = PresentationSummary(shown?.Ships),
            fresh = shown != null && ControlNow < presentationDeadline,
            staleVisualHeld = shown != null && ControlNow >= presentationDeadline,
            alpha = presentationElapsed / 0.05f,
            observed = ready ? PresentationSummary(ObservePresentation()) : null,
            sailCallbacks = presentationInventory?.Select(ship => ship.SailObservations.Select(observation => observation.Value).ToArray()).ToArray(),
            rowerCallbacks = presentationInventory?.Select((ship, slot) => ship.Observations.Select(observation => observation.Value)
                .Where(value => value != null && IsPresentationSample(slot, value.State.Key)).Take(4).ToArray()).ToArray(),
            sailVisualCallbacks = Interlocked.Read(ref sailVisualCallbacks), sailFoldCallbacks = Interlocked.Read(ref sailFoldCallbacks),
            oarVisualCallbacks = Interlocked.Read(ref oarVisualCallbacks),
            semantics = "square-sail morph/yaw and blade local frames; displayed is callback input, observed is native readback; cloth wind remains local; side arrays and observed sail Target/Setting remain local physics diagnostics, not presentation writes; captures are not an atomic native physics cut"
        };
    }
}
#endif
