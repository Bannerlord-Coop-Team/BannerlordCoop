#if DEBUG
using Common.Logging;
using GameInterface.Utils;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms and reports a client-local weapon measurement using the shared water clock.</summary>
public interface INavalWeaponProbe
{
    /// <summary>Arms a future shared-clock window on this client.</summary>
    string Start(CoopBattleController controller, float at, float seconds);
    /// <summary>Ends a partial window with an explicit reason.</summary>
    void Stop(string reason);
    /// <summary>Reports the current window and record counts.</summary>
    object Status();
}

/// <summary>Samples every registered ballista across authority changes without retaining replaced hulls.</summary>
public sealed class NavalWeaponProbe : MissionLogic, INavalWeaponProbe
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalWeaponProbe>();
    private readonly IWaterClock clock;
    private CoopBattleController controller;
    private float start;
    private float end;
    private float last = float.NegativeInfinity;
    private float lastClock = float.NegativeInfinity;
    private int samples;
    private int events;
    private string reason = "idle";

    /// <summary>Uses the clock that drives local battle water, in ten water units per second.</summary>
    public NavalWeaponProbe(IWaterClock clock) => this.clock = clock;
    internal bool IsRunning => reason == "running";

    /// <summary>Arms one future window; the mission retains this probe for later status calls.</summary>
    public string Start(CoopBattleController controller, float at, float seconds)
    {
        if (IsRunning) return "A weapon probe is already running; stop it first.";
        if (!clock.IsRedirected) return "The shared water clock is not redirected; check water_clock.";
        if (!Finite(at) || at <= clock.WrittenTime / 10f) return "at must be a future shared water time in seconds.";
        if (!Finite(seconds) || seconds < 1f || seconds > 300f) return "seconds must be between 1 and 300.";
        this.controller = controller;
        start = at;
        end = at + seconds;
        last = lastClock = float.NegativeInfinity;
        samples = events = 0;
        reason = "running";
        Log("header", Status());
        return null;
    }

    /// <summary>Stops on clock failure or expiry, without inventing samples for missing frames.</summary>
    internal bool InWindow(float time)
    {
        if (!IsRunning) return false;
        if (!clock.IsRedirected) { Stop("clock_lost"); return false; }
        if (!Finite(time)) { Stop("invalid_clock"); return false; }
        if (time < lastClock) { Stop("clock_reversed"); return false; }
        lastClock = time;
        if (time >= end) { Stop("elapsed"); return false; }
        return time >= start;
    }

    /// <summary>Reads live weapon aim, ammunition and skeleton channels at at most 20 Hz.</summary>
    public override void OnMissionTick(float dt)
    {
        float time = clock.WrittenTime / 10f;
        if (!InWindow(time) || time - last < 0.05f) return;
        last = time;
        foreach (var ship in controller.MissionComponent.ShipRegistry.Ships)
        {
            if (ship.Hull is not MissionShip hull || hull.IsRemoved || !hull.GameEntity.IsValid) continue;
            var weapon = hull.ShipSiegeWeapon;
            if (weapon == null || !weapon.GameEntity.IsValid) continue;
            samples++;
            Log("sample", new { identity = Identity(ship, time), weaponId = weapon.Id.Id,
                yawRad = weapon.CurrentDirection, pitchRad = weapon.CurrentReleaseAngle,
                targetYawRad = weapon.TargetDirection, targetPitchRad = weapon.TargetReleaseAngle,
                ammo = weapon.AmmoCount, localState = weapon.State.ToString(),
                animations = weapon.SkeletonOwnerObjects?.Select((skeleton, index) => skeleton == null ? null : new {
                    index, animation = skeleton.GameEntity.Skeleton.GetAnimationIndexAtChannel(0),
                    progress = skeleton.GameEntity.Skeleton.GetAnimationParameterAtChannel(0),
                    paused = skeleton.GameEntity.IsSkeletonAnimationPaused() }).ToArray() });
        }
    }

    /// <summary>Records a successful replay against its stable hull identity, including launches without effects.</summary>
    internal void Shot(MissionObject weapon, bool launchEffects)
    {
        float time = clock.WrittenTime / 10f;
        if (!InWindow(time)) return;
        var ship = controller.MissionComponent.ShipRegistry.Ships.FirstOrDefault(value =>
            value.Hull is MissionShip hull && hull.ShipSiegeWeapon == weapon);
        if (ship == null) return;
        events++;
        Log("event", new { identity = Identity(ship, time), sequence = events, name = "shot_replayed", launchEffects });
    }

    /// <summary>Records actual skeleton animation calls, rather than inferring playback from the copy's simulator state.</summary>
    internal void Animation(SynchedMissionObject skeleton, int? animationIndex)
    {
        float time = clock.WrittenTime / 10f;
        if (!InWindow(time)) return;
        foreach (var ship in controller.MissionComponent.ShipRegistry.Ships)
        {
            var weapon = (ship.Hull as MissionShip)?.ShipSiegeWeapon;
            var skeletons = weapon?.SkeletonOwnerObjects;
            if (skeletons == null) continue;
            int index = Array.IndexOf(skeletons, skeleton);
            if (index < 0) continue;
            events++;
            Log("event", new { identity = Identity(ship, time), sequence = events,
                name = animationIndex.HasValue ? "animation_start" : "animation_resume", skeleton = index,
                animation = animationIndex ?? skeleton.GameEntity.Skeleton.GetAnimationIndexAtChannel(0),
                fireAnimationIndex = AnimationIndex(weapon.FireAnimations, index),
                reloadAnimationIndex = AnimationIndex(weapon.SetUpAnimations, index),
                fireAnimation = weapon.FireAnimations != null && index < weapon.FireAnimations.Length ? weapon.FireAnimations[index] : null,
                reloadAnimation = weapon.SetUpAnimations != null && index < weapon.SetUpAnimations.Length ? weapon.SetUpAnimations[index] : null });
            return;
        }
    }

    /// <summary>Reports the replicated reload state separately from the copy's intentionally unchanged simulator state.</summary>
    internal void AppliedState(MissionObject weapon, BattleShipWeaponState state)
    {
        if (!IsRunning || !weapon.GameEntity.IsValid) return;
        float time = clock.WrittenTime / 10f;
        if (!InWindow(time)) return;
        var ship = controller.MissionComponent.ShipRegistry.Ships.FirstOrDefault(value =>
            value.Hull is MissionShip hull && hull.ShipSiegeWeapon == weapon);
        if (ship == null) return;
        events++;
        Log("event", new { identity = Identity(ship, time), sequence = events, name = "state_applied",
            state = ((RangedSiegeWeapon.WeaponState)state.WeaponState).ToString(), ammo = state.AmmoCount,
            targetYawRad = state.Direction, targetPitchRad = state.ReleaseAngle });
    }

    /// <summary>Resolves configured animation names using the same index lookup as the string playback overload.</summary>
    private int? AnimationIndex(string[] names, int index) => names == null || index >= names.Length || string.IsNullOrEmpty(names[index])
        ? (int?)null : MBAnimation.GetAnimationIndexWithName(names[index]);

    /// <summary>Includes current authority and host on every record so streams remain pairable through migration.</summary>
    private object Identity(NetworkShipInfo ship, float time) => new { instance = controller.Session.InstanceId,
        shipId = ship.ShipId, controller = controller.Session.OwnControllerId, authority = ship.CurrentAuthority,
        host = controller.Session.HostControllerId, hostEpoch = controller.Session.HostEpoch, role = ship.CurrentAuthority == controller.Session.OwnControllerId ? "owner" : "copy",
        timeS = time, missionTimeS = Mission.CurrentTime };

    /// <summary>Preserves a partial window's reason instead of reporting completion.</summary>
    public void Stop(string reason)
    {
        if (!IsRunning) return;
        this.reason = reason;
        Log("end", Status());
    }

    /// <summary>Reports counts and the requested interval without retaining an unbounded sample list.</summary>
    public object Status() => new { reason, instance = controller?.Session.InstanceId, controller = controller?.Session.OwnControllerId,
        startS = start, endS = end, samples, events,
        lastS = float.IsNegativeInfinity(last) ? (float?)null : last };

    /// <summary>Stops before mission objects are released.</summary>
    public override void OnEndMissionInternal() { Stop("mission_end"); base.OnEndMissionInternal(); }

    /// <summary>Rejects non-finite clock and interval inputs.</summary>
    private bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Emits parser-friendly records to this client's ordinary log.</summary>
    private void Log(string type, object data) => Logger.Information("NAVAL_WEAPON {Record:l}", JsonConvert.SerializeObject(new { type, data }));
}
#endif
