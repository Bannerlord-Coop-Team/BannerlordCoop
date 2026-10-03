#if DEBUG
using System;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private MissionMainAgentController deckTurnController;
    private Vec3 deckTurnPreviousLook;
    private Vec3 deckTurnWrittenLook;
    private Vec3 deckSpeedLocal;
    private float deckSpeedTime = -1f;
    private long deckSpeedRevision;
    private int deckSpeedSupport = -1;
    private float deckSpeedValue;

    // The pose is recomputed from the actual position on every capture; only the support frame changes when boarding.
    internal bool TryCaptureOwnCaptainDeck(Agent captain, Vec3 worldPosition, out int supportSlot, out Vec3 deckLocal,
        out float deckSpeed)
    {
        deckLocal = Vec3.Zero;
        deckSpeed = 0f;
        supportSlot = -1;
        if (manifest.AllPhysicsProbe || Mission == null || captain == null
            || captain != LocalCaptain || captain != Mission.MainAgent || offeredHelm == null || offeredHelm.Occupied
            || HasOccupiedLocalHelm()) return ResetDeckSpeed();
        supportSlot = CaptainSupportSlot(captain);
        if (supportSlot < 0 || !IsFixtureHull(Ships[supportSlot]) || !TryGetValidHullFrame(Ships[supportSlot], out var hullFrame))
            return ResetDeckSpeed();
        deckLocal = hullFrame.TransformToLocalNonOrthogonal(worldPosition);
        if (!deckLocal.IsValid) return ResetDeckSpeed();
        deckSpeed = DeckSpeed(deckLocal, supportSlot, offeredHelm.Revision);
        return true;
    }

    // Any fixture hull can carry the captain; a connected fixture plank maps to its rope source hull.
    private int CaptainSupportSlot(Agent captain)
    {
        var stepped = captain.GetComponent<AgentNavalComponent>()?.SteppedShip;
        int slot = stepped == null ? -1 : Array.IndexOf(Ships, stepped);
        return slot >= 0 && stepped.GetIsAgentOnShip(captain) ? slot : PlankSupportSlot(captain);
    }

    private bool ResetDeckSpeed()
    {
        deckSpeedTime = -1f;
        return false;
    }

    // The captain slot carries identity; the support slot only selects the reference hull frame.
    internal bool TryGetCaptainDeckFrame(int captainSlot, int supportSlot, Agent captain, out MatrixFrame hullFrame)
    {
        hullFrame = default;
        if (manifest.AllPhysicsProbe || terminal || nativeTerminalHold || Blocker != null
            || Mission == null || Mission != Mission.Current || captainSlot == OwnSlot || captainSlot < 0
            || captainSlot >= Ships.Length || supportSlot < 0 || supportSlot >= Ships.Length || captain == null
            || Agents[captainSlot * NavalLabManifest.CrewPerShip] != captain) return false;
        var ship = Ships[supportSlot];
        return IsFixtureHull(ship) && TryGetValidHullFrame(ship, out hullFrame);
    }

    private bool IsFixtureHull(MissionShip ship) => ship != null && ship.ShipOrigin is NavalLabShipOrigin
        && ship.ShipOrigin.Hull == hull && ship.IsDeployed && ship.GameEntity.IsValid;

    private static bool TryGetValidHullFrame(MissionShip ship, out MatrixFrame frame)
    {
        frame = ship.GlobalFrame;
        return NetworkNavalLabOarPresentation.ValidFrame(NetworkNavalLabOarPresentation.FromFrame(frame));
    }

    // Finite difference of hull-local positions, so the remote throttle never includes hull motion.
    private float DeckSpeed(Vec3 local, int supportSlot, long revision)
    {
        float now = Mission.CurrentTime;
        float dt = now - deckSpeedTime;
        if (deckSpeedTime < 0f || revision != deckSpeedRevision || supportSlot != deckSpeedSupport || dt < 0f || dt > 0.5f)
            deckSpeedValue = 0f;
        else if (dt < 0.005f) return deckSpeedValue;
        else deckSpeedValue = (local - deckSpeedLocal).AsVec2.Length / dt;
        deckSpeedLocal = local;
        deckSpeedTime = now;
        deckSpeedRevision = revision;
        deckSpeedSupport = supportSlot;
        return deckSpeedValue;
    }

    // Synthetic walk/turn is allowed only for the own captain after a confirmed, stable helm release.
    internal string DeckLocomotionBlocker()
    {
        if (manifest.AllPhysicsProbe) return "wrong_mode";
        if (Mission == null || Mission != Mission.Current || !CanUseNativeControls || terminal || nativeTerminalHold
            || Blocker != null) return "fixture_not_ready_or_terminal";
        if (!nativeAutoHelmObserved || nativeHelmPhase == "requested" || nativeHelmPhase == "pending"
            || nativeHelmPhase == "failed") return "native_helm_transition";
        if (offeredHelm == null || offeredHelm.Occupied || !HelmReplicasReady) return "helm_release_not_confirmed";
        if (pulsePending) return "control_active";
        try
        {
            if (ObserveHelmState(OwnSlot, ReplicatedHelmMachine(OwnSlot))) return "helm_occupied";
        }
        catch (InvalidOperationException) { return "helm_identity"; }
        if (LocalCaptain.HasMount || Mission.GetMissionBehavior<MissionMainAgentController>() == null)
            return "captain_or_controller_unavailable";
        return null;
    }

    // The main-agent controller rewrites look from the camera each frame unless CustomLookDir is set.
    private void StartDeckTurn(Agent captain)
    {
        deckTurnController = Mission.GetMissionBehavior<MissionMainAgentController>();
        deckTurnPreviousLook = deckTurnController.CustomLookDir;
        Vec3 look = captain.LookDirection;
        look.z = 0f;
        deckTurnWrittenLook = look.LengthSquared > 0.0001f ? look.NormalizedCopy() : new Vec3(0f, 1f, 0f);
        deckTurnController.CustomLookDir = deckTurnWrittenLook;
    }

    private void TickDeckTurn(float dt)
    {
        deckTurnWrittenLook.RotateAboutZ(controlValue * Math.Min(dt, 0.1f));
        deckTurnController.CustomLookDir = deckTurnWrittenLook;
    }

    private void StopDeckTurn()
    {
        if (deckTurnController == null) return;
        deckTurnController.CustomLookDir = deckTurnPreviousLook;
        deckTurnController = null;
    }
}
#endif
