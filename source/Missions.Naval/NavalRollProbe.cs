#if DEBUG
using Common.Logging;
using GameInterface.Utils;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using Serilog;
using System;

namespace Missions.Naval;

/// <summary>A client-local, read-only roll measurement driven by the existing copy probe's mission tick.</summary>
public interface INavalRollProbe
{
    bool IsRunning { get; }
    /// <summary>Arms a future shared-clock window for one registry identity.</summary>
    string Start(NetworkShipInfo ship, string instance, string controller, float at, float seconds);
    /// <summary>Reads one live hull pose when its sampling interval is due.</summary>
    void Tick();
    /// <summary>Ends a running interval with an explicit completion or failure reason.</summary>
    void Stop(string reason);
    /// <summary>Reports the requested window, count, local means and completion reason.</summary>
    object Status();
}

/// <summary>Logs signed hull angles against the shared water clock, reusing the copy diagnostics' rolling windows.</summary>
public sealed class NavalRollProbe : INavalRollProbe
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalRollProbe>();
    private readonly IWaterClock clock;
    private NetworkShipInfo ship;
    private MissionShip hull;
    private string authority;
    private string instance;
    private string controller;
    private float start;
    private float end;
    private float last = float.NegativeInfinity;
    private RollingWindow roll;
    private RollingWindow pitch;
    private RollingWindow heave;
    private string reason = "idle";

    /// <summary>Uses the same redirected clock that drives this client's battle waves.</summary>
    public NavalRollProbe(IWaterClock clock) => this.clock = clock;

    public bool IsRunning => reason == "running";

    /// <summary>Arms one future interval in shared water seconds; refuses to replace a running measurement.</summary>
    public string Start(NetworkShipInfo ship, string instance, string controller, float at, float seconds)
    {
        if (reason == "running") return "A roll probe is already running; stop it first.";
        if (!clock.IsRedirected) return "The shared water clock is not redirected; check water_clock.";
        if (float.IsNaN(at) || float.IsInfinity(at) || at <= clock.WrittenTime / 10f)
            return "at must be a future shared water time in seconds (water_clock written / 10).";
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 1f || seconds > 300f)
            return "seconds must be between 1 and 300.";

        this.ship = ship;
        hull = ship.Hull as MissionShip;
        authority = ship.CurrentAuthority;
        this.instance = instance;
        this.controller = controller;
        start = at;
        end = at + seconds;
        last = float.NegativeInfinity;
        roll = new RollingWindow(seconds);
        pitch = new RollingWindow(seconds);
        heave = new RollingWindow(seconds);
        reason = "running";
        Log("header", Status());
        return null;
    }

    /// <summary>Records the selected hull at most 20 times per shared second, stopping on identity or clock changes.</summary>
    public void Tick()
    {
        if (reason != "running") return;
        if (!clock.IsRedirected) { Stop("clock_lost"); return; }
        if (ship.CurrentAuthority != authority || ship.Hull != hull) { Stop("authority_or_hull_changed"); return; }
        if (hull == null || hull.IsRemoved || !hull.GameEntity.IsValid) { Stop("hull_removed"); return; }

        var frame = hull.GlobalFrame;
        Record(clock.WrittenTime / 10f, NavalWaterProbeLogic.AxisTiltDegrees(frame.rotation.s.z),
            NavalWaterProbeLogic.AxisTiltDegrees(frame.rotation.f.z), frame.origin.z);
    }

    /// <summary>Records actual timestamps without filling missed ticks; offline pairing can reject gaps rather than inventing data.</summary>
    internal void Record(float time, float rollDegrees, float pitchDegrees, float heaveMetres)
    {
        if (reason != "running") return;
        if (!Finite(time) || !Finite(rollDegrees) || !Finite(pitchDegrees) || !Finite(heaveMetres))
        { Stop("invalid_sample"); return; }
        if (time < last) { Stop("clock_reversed"); return; }
        if (time >= end) { Stop("elapsed"); return; }
        if (time < start || time - last < 0.05f) return;

        last = time;
        roll.Add(time, rollDegrees);
        pitch.Add(time, pitchDegrees);
        heave.Add(time, heaveMetres);
        Log("sample", new { instance, shipId = ship.ShipId, controller, authority,
            role = controller == authority ? "owner" : "copy", timeS = time,
            rollDeg = rollDegrees, pitchDeg = pitchDegrees, heaveM = heaveMetres });
    }

    /// <summary>Ends the interval and logs its count and local summaries; partial intervals retain their stop reason.</summary>
    public void Stop(string reason)
    {
        if (this.reason != "running") return;
        this.reason = reason;
        Log("end", Status());
    }

    /// <summary>Describes the interval and local summaries, which are not paired copy-minus-owner statistics.</summary>
    public object Status() => new { reason, instance, shipId = ship?.ShipId, controller, authority,
        startS = start, endS = end, samples = roll?.Count ?? 0,
        lastS = float.IsNegativeInfinity(last) ? (float?)null : last,
        rollMeanDeg = roll?.Mean, pitchMeanDeg = pitch?.Mean, heaveMeanM = heave?.Mean };

    /// <summary>Rejects non-finite measurements instead of silently replacing them with zero.</summary>
    private bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Writes one parser-friendly JSON record for offline pairing with the other client's log.</summary>
    private void Log(string type, object data) => Logger.Information("NAVAL_ROLL {Record:l}", JsonConvert.SerializeObject(new { type, data }));
}
#endif
