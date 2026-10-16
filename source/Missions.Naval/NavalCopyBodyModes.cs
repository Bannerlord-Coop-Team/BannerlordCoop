#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>DEBUG: how another owner's hull exists on this client. In every mode its owner stays the authority.</summary>
public enum CopyBodyMode
{
    /// <summary>The default copy: its dynamic body is disabled at spawn and the owner's frame is written to it.</summary>
    Kinematic = 0,

    /// <summary>
    /// Option B: the dynamic body keeps simulating, replays the owner's helm input and is pushed toward the owner's samples
    /// by a velocity correction instead of frame writes.
    /// </summary>
    Steered = 1,

    /// <summary>Option C: the frame-written copy's body is re-added as a moveable static collider.</summary>
    Static = 2,
}

/// <summary>DEBUG: the steered copy's correction knobs, as <c>key=value</c> arguments of <c>coop.debug.naval.copy_body</c>.</summary>
public sealed class CopySteeringKnobs
{
    internal const string KnobNames = "gain window deadzone clamp tau yawdeadzone yawclamp snap";

    /// <summary>Share of the position error the target velocity closes per <see cref="WindowSeconds"/>.</summary>
    public float Gain { get; private set; } = 1f;

    /// <summary>Seconds over which the target velocity closes the position and heading error.</summary>
    public float WindowSeconds { get; private set; } = 0.5f;

    /// <summary>Planar position error in metres below which no linear correction is applied.</summary>
    public float DeadZoneMetres { get; private set; } = 0.25f;

    /// <summary>Largest correction speed in m/s added on top of the owner's velocity.</summary>
    public float ClampMetresPerSecond { get; private set; } = 4f;

    /// <summary>Time constant in seconds over which the body's velocity is blended to the target velocity.</summary>
    public float TauSeconds { get; private set; } = 0.2f;

    /// <summary>Heading error in degrees below which no yaw correction is applied.</summary>
    public float YawDeadZoneDegrees { get; private set; } = 1f;

    /// <summary>Largest yaw correction rate in degrees per second added on top of the owner's yaw rate.</summary>
    public float YawClampDegreesPerSecond { get; private set; } = 10f;

    /// <summary>Planar position error in metres beyond which the copy is teleported to the owner's frame.</summary>
    public float SnapMetres { get; private set; } = 25f;

    public CopySteeringKnobs Copy() => (CopySteeringKnobs)MemberwiseClone();

    /// <summary>Applies one knob; why it was refused, or null.</summary>
    public string TryApply(string key, string value)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "gain": return Set(value, 0f, 10f, "gain", v => Gain = v);
            case "window": return Set(value, 0.05f, 5f, "window (s)", v => WindowSeconds = v);
            case "deadzone": return Set(value, 0f, 10f, "deadzone (m)", v => DeadZoneMetres = v);
            case "clamp": return Set(value, 0f, 30f, "clamp (m/s)", v => ClampMetresPerSecond = v);
            case "tau": return Set(value, 0.01f, 5f, "tau (s)", v => TauSeconds = v);
            case "yawdeadzone": return Set(value, 0f, 45f, "yawdeadzone (deg)", v => YawDeadZoneDegrees = v);
            case "yawclamp": return Set(value, 0f, 90f, "yawclamp (deg/s)", v => YawClampDegreesPerSecond = v);
            case "snap": return Set(value, 1f, 500f, "snap (m)", v => SnapMetres = v);
            default: return $"Unknown knob {key}; knobs: {KnobNames}.";
        }
    }

    private static string Set(string value, float min, float max, string name, Action<float> assign)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed)
            || parsed < min || parsed > max)
            return $"{name} must be a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.";

        assign(parsed);
        return null;
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "gain={0} window={1} deadzone={2} clamp={3} tau={4} yawdeadzone={5} yawclamp={6} snap={7}",
        Gain, WindowSeconds, DeadZoneMetres, ClampMetresPerSecond, TauSeconds, YawDeadZoneDegrees, YawClampDegreesPerSecond,
        SnapMetres);
}

/// <summary>DEBUG: one parsed <c>coop.debug.naval.copy_body</c> call: which copies, which mode and the steering knobs.</summary>
public sealed class CopyBodyRequest
{
    internal const string AllTarget = "all";

    internal const string Usage = "Usage: coop.debug.naval.copy_body <shipId-prefix|all> kinematic|steered|static [key=value...] " +
        "(steered knobs: " + CopySteeringKnobs.KnobNames + ")";

    private CopyBodyRequest(string target, CopyBodyMode mode, CopySteeringKnobs knobs)
    {
        Target = target;
        Mode = mode;
        Knobs = knobs;
    }

    /// <summary><see cref="AllTarget"/> or a ship id prefix.</summary>
    public string Target { get; }

    public bool IsAll => Target == AllTarget;

    public CopyBodyMode Mode { get; }

    public CopySteeringKnobs Knobs { get; }

    /// <summary>Parses the command arguments; why they were refused, or null.</summary>
    public static string TryParse(IReadOnlyList<string> args, out CopyBodyRequest request)
    {
        request = null;
        if (args == null || args.Count < 2) return Usage;

        string target = args[0]?.Trim();
        if (string.IsNullOrEmpty(target)) return "shipId-prefix must not be empty. " + Usage;
        if (string.Equals(target, AllTarget, StringComparison.OrdinalIgnoreCase)) target = AllTarget;

        if (!TryParseMode(args[1], out var mode)) return $"Unknown mode {args[1]}; modes: kinematic, steered, static.";

        var knobs = new CopySteeringKnobs();
        foreach (var arg in args.Skip(2))
        {
            int separator = arg?.IndexOf('=') ?? -1;
            if (separator <= 0) return $"Expected key=value, got {arg}. {Usage}";
            if (mode != CopyBodyMode.Steered) return $"{arg}: knobs apply to steered copies only.";

            var error = knobs.TryApply(arg.Substring(0, separator), arg.Substring(separator + 1));
            if (error != null) return error;
        }

        request = new CopyBodyRequest(target, mode, knobs);
        return null;
    }

    internal static bool TryParseMode(string value, out CopyBodyMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "kinematic":
                mode = CopyBodyMode.Kinematic;
                return true;
            case "steered":
                mode = CopyBodyMode.Steered;
                return true;
            case "static":
                mode = CopyBodyMode.Static;
                return true;
            default:
                mode = CopyBodyMode.Kinematic;
                return false;
        }
    }

    /// <summary>
    /// The registered copies the request names, given every registered hull and whether this client owns it. Refuses an own
    /// hull and an ambiguous prefix; a prefix that matches nothing yet is kept for a copy that spawns later.
    /// </summary>
    public string ResolveCopies(IEnumerable<(Guid ShipId, bool IsOwn)> ships, out IReadOnlyList<Guid> copies)
    {
        var registered = ships.ToList();
        if (IsAll)
        {
            copies = registered.Where(ship => !ship.IsOwn).Select(ship => ship.ShipId).ToList();
            return null;
        }

        copies = Array.Empty<Guid>();
        var matches = registered.Where(ship => Matches(ship.ShipId)).ToList();
        if (matches.Count > 1) return $"Several registered hulls' ship ids start with {Target}; give more of the id.";
        if (matches.Count == 1 && matches[0].IsOwn)
            return $"Hull {matches[0].ShipId} is this client's own hull. Its owner stays the authority; copy_body only changes " +
                "how another owner's hull exists here.";

        copies = matches.Select(ship => ship.ShipId).ToList();
        return null;
    }

    public bool Matches(Guid shipId) => IsAll || shipId.ToString("D").StartsWith(Target, StringComparison.OrdinalIgnoreCase);
}

/// <summary>DEBUG: the native body operations a mode switch needs on one copy.</summary>
internal interface ICopyBodyDriver<THull>
{
    /// <summary>Whether the hull's dynamic body still simulates.</summary>
    bool IsSimulating(THull hull);

    /// <summary>Turns the simulation of the hull's dynamic body off; the engine has no inverse.</summary>
    void DisableSimulation(THull hull);

    /// <summary>Re-adds the hull's body as a moveable static collider; why it could not, or null.</summary>
    string MakeStatic(THull hull);

    /// <summary>Gives a static hull its dynamic body back, with its simulation off.</summary>
    void RestoreFromStatic(THull hull);
}

/// <summary>DEBUG: one tracked copy: its ship id, current mode, the steering state and why a requested mode could not apply.</summary>
internal sealed class CopyBodyEntry
{
    public CopyBodyEntry(Guid shipId, CopyBodyMode mode)
    {
        ShipId = shipId;
        Mode = mode;
    }

    public Guid ShipId { get; }

    public CopyBodyMode Mode { get; set; }

    /// <summary>Why the last requested mode did not apply, or null.</summary>
    public string Blocked { get; set; }

    public CopyBodyMode? BlockedMode { get; set; }

    /// <summary>The correction while steered; null otherwise.</summary>
    public CopySteering Steering { get; set; }

    /// <summary>The owner's latest frame as the replicator would write it, in every mode; null before the first.</summary>
    public MatrixFrame? OwnerFrame { get; set; }

    public CopyDiagnostics Diagnostics { get; } = new CopyDiagnostics();
}

/// <summary>
/// [Game thread] DEBUG: the copy_body rules of this mission and the mode each copy is in. A later rule overrides an
/// earlier one for the ships it names, and <c>all</c> replaces every rule. Every switch goes through kinematic, which is
/// also what removal and <see cref="Clear"/> leave a copy in.
/// </summary>
internal sealed class CopyBodyModeTable<THull> where THull : class
{
    private readonly ICopyBodyDriver<THull> driver;
    private readonly List<CopyBodyRequest> rules = new List<CopyBodyRequest>();
    private readonly Dictionary<THull, CopyBodyEntry> entries = new Dictionary<THull, CopyBodyEntry>();

    public CopyBodyModeTable(ICopyBodyDriver<THull> driver)
    {
        this.driver = driver;
    }

    public IReadOnlyDictionary<THull, CopyBodyEntry> Entries => entries;

    public IReadOnlyList<CopyBodyRequest> Rules => rules;

    public void AddRule(CopyBodyRequest request)
    {
        if (request.IsAll) rules.Clear();
        rules.Add(request);
    }

    /// <summary>The last rule naming the ship, or null when none does (kinematic).</summary>
    public CopyBodyRequest RuleFor(Guid shipId) => rules.LastOrDefault(rule => rule.Matches(shipId));

    public CopyBodyMode ModeFor(Guid shipId) => RuleFor(shipId)?.Mode ?? CopyBodyMode.Kinematic;

    public bool IsTracked(THull hull) => hull != null && entries.ContainsKey(hull);

    /// <summary>Starts tracking a copy in the mode its body is in: a still simulating body is steered.</summary>
    public CopyBodyEntry Track(THull hull, Guid shipId)
    {
        if (entries.TryGetValue(hull, out var existing)) return existing;

        var entry = new CopyBodyEntry(shipId, driver.IsSimulating(hull) ? CopyBodyMode.Steered : CopyBodyMode.Kinematic);
        entries.Add(hull, entry);
        if (entry.Mode == CopyBodyMode.Steered) entry.Steering = new CopySteering(KnobsFor(shipId));
        return entry;
    }

    /// <summary>Moves a tracked copy to the mode its rule asks for, where the engine allows.</summary>
    public void Reconcile(THull hull)
    {
        if (!entries.TryGetValue(hull, out var entry)) return;

        var desired = ModeFor(entry.ShipId);
        if (desired == entry.Mode)
        {
            entry.Blocked = null;
            entry.BlockedMode = null;
            if (entry.Mode == CopyBodyMode.Steered) entry.Steering.Knobs = KnobsFor(entry.ShipId);
            return;
        }

        if (entry.BlockedMode == desired) return;

        switch (desired)
        {
            case CopyBodyMode.Kinematic:
                Revert(hull, entry);
                return;
            case CopyBodyMode.Static:
                Revert(hull, entry);
                var error = driver.MakeStatic(hull);
                if (error != null)
                {
                    Block(entry, desired, error);
                    return;
                }

                entry.Mode = CopyBodyMode.Static;
                entry.Blocked = null;
                entry.BlockedMode = null;
                return;
            case CopyBodyMode.Steered:
                Block(entry, desired, "the copy's dynamic body was disabled when it became a copy, and the engine cannot turn it back " +
                    "on; run copy_body before the copy spawns (during deployment)");
                return;
        }
    }

    public void ReconcileAll()
    {
        foreach (var hull in entries.Keys.ToList())
            Reconcile(hull);
    }

    /// <summary>Puts a copy that leaves the mission back into kinematic and stops tracking it.</summary>
    public void OnRemoving(THull hull)
    {
        if (hull == null || !entries.TryGetValue(hull, out var entry)) return;

        Revert(hull, entry);
        entries.Remove(hull);
    }

    /// <summary>Puts every tracked copy back into kinematic and drops every rule, as the mission ends.</summary>
    public void Clear()
    {
        foreach (var entry in entries.ToList())
            Revert(entry.Key, entry.Value);

        entries.Clear();
        rules.Clear();
    }

    private void Revert(THull hull, CopyBodyEntry entry)
    {
        if (entry.Mode == CopyBodyMode.Steered) driver.DisableSimulation(hull);
        else if (entry.Mode == CopyBodyMode.Static) driver.RestoreFromStatic(hull);

        entry.Mode = CopyBodyMode.Kinematic;
        entry.Steering = null;
    }

    private static void Block(CopyBodyEntry entry, CopyBodyMode desired, string reason)
    {
        entry.Blocked = reason;
        entry.BlockedMode = desired;
    }

    private CopySteeringKnobs KnobsFor(Guid shipId) => RuleFor(shipId)?.Knobs?.Copy() ?? new CopySteeringKnobs();
}
#endif
