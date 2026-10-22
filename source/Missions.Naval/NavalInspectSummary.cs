using Missions.Battles;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>The compact <c>coop.debug.naval.inspect summary</c>: a header with the session and deck error, then one line per hull.</summary>
internal static class NavalInspectSummary
{
    internal const string Label = "NAVAL_INSPECT_SUMMARY";

    /// <summary>[Game thread] The header and one line per registered hull of the local battle.</summary>
    internal static string Describe(Mission mission, CoopBattleController controller)
    {
        var naval = mission.GetMissionBehavior<CoopNavalBattleBehavior>();
        var hulls = controller.MissionComponent.ShipRegistry.Ships
            .Where(ship => ship.Hull is MissionShip)
            .Select(ship => Summarize(mission, naval, ship))
            .ToList();

        var text = new StringBuilder(Header(controller, hulls.Count));
        foreach (var hull in hulls) text.Append('\n').Append(FormatHull(hull));
        return text.ToString();
    }

    /// <summary>One hull's line: id, hull type, owner, authority, HP, sail HP, sinking, copy mode, drift and fates sent.</summary>
    internal static string FormatHull(HullSummary hull)
    {
        var line = new StringBuilder();
        line.Append(hull.ShortId).Append(' ').Append(hull.HullType ?? "?")
            .Append(" owner=").Append(hull.Owner ?? "?").Append(hull.IsNpc ? "(npc)" : "")
            .Append(" auth=").Append(hull.Authority ?? "?")
            .Append(" hp=").Append(Number(hull.HitPoints)).Append('/').Append(Number(hull.MaxHitPoints))
            .Append(" sail=").Append(Number(hull.SailHitPoints)).Append('/').Append(Number(hull.MaxSailHitPoints))
            .Append(' ').Append(hull.Sinking)
            .Append(" copy=").Append(hull.CopyMode);
        if (hull.DriftMean.HasValue)
            line.Append(" drift=").Append(Metres(hull.DriftMean.Value)).Append('/').Append(Metres(hull.DriftMax ?? 0f));
        if (hull.FatesSent.HasValue)
            line.Append(" fatesSent=").Append(hull.FatesSent.Value.ToString(CultureInfo.InvariantCulture));
        return line.ToString();
    }

    /// <summary>Whether the arguments ask for the summary (<c>summary</c> or <c>brief</c>) or the full output (none); false when invalid.</summary>
    internal static bool TryParseMode(IReadOnlyList<string> args, out bool summary, out string error)
    {
        summary = false;
        error = null;
        if (args == null || args.Count == 0) return true;

        if (args.Count == 1 && (IsMode(args[0], "summary") || IsMode(args[0], "brief")))
        {
            summary = true;
            return true;
        }

        error = "Usage: coop.debug.naval.inspect [summary|brief]";
        return false;
    }

    // Whether an argument names the mode, ignoring case and surrounding spaces.
    private static bool IsMode(string arg, string mode) => string.Equals(arg?.Trim(), mode, StringComparison.OrdinalIgnoreCase);

    // The session line: instance, own id, host role and epoch, deployment, hull count, deck error and copy-on-copy contacts.
    private static string Header(CoopBattleController controller, int hullCount)
    {
        var decks = JObject.FromObject(controller.MissionComponent.AgentMovementHandler.InspectShipDecks());
        var interpolator = decks["interpolator"];
        return Label + " instance=" + controller.Session.InstanceId + " own=" + controller.Session.OwnControllerId +
            " host=" + controller.Session.IsLocalHost + " epoch=" + controller.Session.HostEpoch +
            " deployed=" + controller.Deployment.IsCommitted + " hulls=" + hullCount +
            " deckError=" + interpolator?["lastDeckError"] + "/" + interpolator?["maxDeckError"] +
            CopyOnCopy(controller);
    }

#if DEBUG
    // DEBUG: the copy-on-copy contact callbacks, which the copy pair filter keeps at 0.
    private static string CopyOnCopy(CoopBattleController controller)
    {
        var hulls = controller.MissionComponent.ShipRegistry.Ships
            .Where(ship => ship.Hull is MissionShip hull && !hull.IsRemoved && hull.GameEntity.IsValid)
            .Select(ship => (ship.ShipId, (MissionShip)ship.Hull));
        var contact = JObject.FromObject(NavalContactProbe.Inspect(hulls));
        return " copyOnCopy=" + contact["contactCallbacks"]?["copyOnCopy"];
    }
#else
    // Release has no contact probe.
    private static string CopyOnCopy(CoopBattleController controller) => "";
#endif

    // Reads one registered hull into its summary line's fields; drift and fates sent are DEBUG only.
    private static HullSummary Summarize(Mission mission, CoopNavalBattleBehavior naval, NetworkShipInfo info)
    {
        var hull = (MissionShip)info.Hull;
        float? driftMean = null;
        float? driftMax = null;
        int? fatesSent = null;
#if DEBUG
        var probe = mission.GetMissionBehavior<NavalCopyProbeLogic>();
        if (probe != null && probe.SteeredCopies.Copies.TryGetValue(hull, out var copy) && copy.Diagnostics.Distance.Count > 0)
        {
            driftMean = copy.Diagnostics.Distance.Mean;
            driftMax = copy.Diagnostics.Distance.Max;
        }
        if (naval != null) fatesSent = naval.FateReporter.FatesSentOf(info.ShipId);
#endif
        return new HullSummary(
            ShortId(info.ShipId), hull.ShipOrigin.Hull?.StringId, info.OriginalOwner, info.CurrentAuthority, info.IsNpcParty,
            hull.HitPoints, hull.ShipOrigin.MaxHitPoints, hull.SailHitPoints, hull.ShipOrigin.MaxSailHitPoints,
            hull.IsSunk ? "sunk" : hull.IsSinking ? "sinking" : "afloat", NavalSteeredCopies.ModeName(hull),
            driftMean, driftMax, fatesSent);
    }

    /// <summary>The first 8 hex digits of a ship id, which the hull commands take as a prefix.</summary>
    internal static string ShortId(Guid shipId) => shipId.ToString("N").Substring(0, 8);

    // HP with at most one decimal.
    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    // Metres with two decimals.
    private static string Metres(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The fields of one hull's summary line.</summary>
    internal readonly struct HullSummary
    {
        // Assigns every field of the line.
        public HullSummary(string shortId, string hullType, string owner, string authority, bool isNpc, float hitPoints,
            float maxHitPoints, float sailHitPoints, float maxSailHitPoints, string sinking, string copyMode, float? driftMean,
            float? driftMax, int? fatesSent)
        {
            ShortId = shortId;
            HullType = hullType;
            Owner = owner;
            Authority = authority;
            IsNpc = isNpc;
            HitPoints = hitPoints;
            MaxHitPoints = maxHitPoints;
            SailHitPoints = sailHitPoints;
            MaxSailHitPoints = maxSailHitPoints;
            Sinking = sinking;
            CopyMode = copyMode;
            DriftMean = driftMean;
            DriftMax = driftMax;
            FatesSent = fatesSent;
        }

        public string ShortId { get; }
        public string HullType { get; }
        public string Owner { get; }
        public string Authority { get; }
        public bool IsNpc { get; }
        public float HitPoints { get; }
        public float MaxHitPoints { get; }
        public float SailHitPoints { get; }
        public float MaxSailHitPoints { get; }
        public string Sinking { get; }
        public string CopyMode { get; }
        public float? DriftMean { get; }
        public float? DriftMax { get; }
        public int? FatesSent { get; }
    }
}
