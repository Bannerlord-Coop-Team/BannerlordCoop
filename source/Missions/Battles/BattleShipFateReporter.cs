using Common.Logging;
using Common.Network;
using GameInterface.Services.Ships.Data;
using GameInterface.Services.Ships.Messages;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Reports what the battle did to the campaign ship of each hull this client simulates: HP, sail HP, sinking and
/// mitigated damage. Sinking goes out at once, other changes at most every 2 s; removals, results and mission end flush the rest.
/// </summary>
public interface IBattleShipFateReporter
{
    /// <summary>[Game thread] Sends a hull's fate at once when it starts sinking, and the other changed fates once the interval passed.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Sends the fate of an own hull that leaves the mission, if it changed since the last one sent.</summary>
    void ReportRemoval(MissionObject hull);

    /// <summary>[Game thread] Sends every own hull's changed fate now, ahead of a result report or the mission's end.</summary>
    void Flush();

    /// <summary>How many fates of a ship this client sent (diagnostics).</summary>
    int FatesSentOf(Guid shipId);
}

/// <inheritdoc cref="IBattleShipFateReporter"/>
public class BattleShipFateReporter : IBattleShipFateReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipFateReporter>();

    internal const float ReportIntervalSeconds = 2f;
    // Changes below this are float noise, not damage.
    private const float Tolerance = 0.01f;

    private readonly INetwork relayNetwork;
    private readonly IBattleSession session;
    private readonly INetworkShipRegistry registry;
    private readonly INavalShipEngine engine;
    private readonly Dictionary<Guid, HullReport> reports = new Dictionary<Guid, HullReport>();
    private float sinceLastSend;
    private long revision;

    public BattleShipFateReporter(INetwork relayNetwork, IBattleSession session, INetworkShipRegistry registry,
        INavalShipEngine engine)
    {
        this.relayNetwork = relayNetwork;
        this.session = session;
        this.registry = registry;
        this.engine = engine;
    }

    public void Tick(float dt)
    {
        sinceLastSend += Math.Max(0f, dt);
        bool due = sinceLastSend >= ReportIntervalSeconds;
        if (!due && !registry.Ships.Any(IsNewlySinking)) return;

        // The interval restarts whether or not anything changed, so an idle battle reads its hulls every 2 s, not every frame.
        if (due) sinceLastSend = 0f;
        Send(ChangedFates(registry.Ships));
    }

    public void ReportRemoval(MissionObject hull)
    {
        if (!registry.TryGetByHull(hull, out var ship)) return;

        Send(ChangedFates(new[] { ship }));
    }

    public void Flush() => Send(ChangedFates(registry.Ships));

    public int FatesSentOf(Guid shipId) => reports.TryGetValue(shipId, out var report) ? report.Sent : 0;

    // An own live hull that started sinking since its last fate went out, which the server must hear about at once.
    private bool IsNewlySinking(NetworkShipInfo ship) =>
        session.IsOwn(ship.CurrentAuthority) && !engine.IsHullRemoved(ship.Hull) && engine.IsSinking(ship.Hull)
        && !(reports.TryGetValue(ship.ShipId, out var report) && report.Last?.Sunk == true)
        && !string.IsNullOrEmpty(engine.CampaignShipIdOf(ship.Hull));

    // The fates of own live hulls that stand for a campaign ship and differ from the last one sent; a sunk ship is final.
    private List<PendingFate> ChangedFates(IEnumerable<NetworkShipInfo> ships)
    {
        var changed = new List<PendingFate>();
        foreach (var ship in ships)
        {
            if (!session.IsOwn(ship.CurrentAuthority) || engine.IsHullRemoved(ship.Hull)) continue;

            reports.TryGetValue(ship.ShipId, out var report);
            var fate = ReadFate(ship.Hull, report?.ReportedMitigatedDamage ?? 0f);
            if (fate == null) continue;
            if (report?.Last != null && (report.Last.Sunk || !Differs(report.Last, fate))) continue;

            changed.Add(new PendingFate(ship.ShipId, fate));
        }

        return changed;
    }

    // The hull's fate with the damage mitigated since the last fate this client sent for it.
    private BattleShipFate ReadFate(MissionObject hull, float reportedMitigatedDamage)
    {
        var campaignShipId = engine.CampaignShipIdOf(hull);
        var condition = engine.ReadCondition(hull);
        if (string.IsNullOrEmpty(campaignShipId) || condition == null) return null;

        return FateOf(campaignShipId, condition, Math.Max(0f, engine.ReadMitigatedDamage(hull) - reportedMitigatedDamage));
    }

    /// <summary>The fate a hull's condition stands for: vanilla destroys a ship at 0 HP, so a sinking hull counts as sunk too.</summary>
    internal static BattleShipFate FateOf(string campaignShipId, BattleShipCondition condition, float mitigatedDamage = 0f)
    {
        bool sunk = condition.SinkingState >= BattleShipCondition.Sinking || condition.HitPoints <= 0f;
        return new BattleShipFate(campaignShipId, condition.HitPoints, condition.SailHitPoints, sunk, mitigatedDamage);
    }

    // Whether the server must hear about this fate: its sunk state, HP or sail HP moved, or new damage was mitigated.
    private static bool Differs(BattleShipFate previous, BattleShipFate fate) =>
        previous.Sunk != fate.Sunk
        || Math.Abs(previous.HitPoints - fate.HitPoints) > Tolerance
        || Math.Abs(previous.SailHitPoints - fate.SailHitPoints) > Tolerance
        || fate.MitigatedDamage > Tolerance;

    // Sends the changed fates as one numbered message at this client's host epoch and remembers each as the last one sent.
    private void Send(IReadOnlyList<PendingFate> changed)
    {
        if (changed.Count == 0 || !session.HasInstance) return;

        revision++;
        relayNetwork.SendAll(new NetworkBattleShipFates(session.InstanceId, session.HostEpoch, revision,
            changed.Select(change => change.Fate).ToArray()));
        sinceLastSend = 0f;

        foreach (var change in changed)
        {
            if (!reports.TryGetValue(change.ShipId, out var report))
            {
                report = new HullReport();
                reports.Add(change.ShipId, report);
            }

            report.Last = change.Fate;
            report.ReportedMitigatedDamage += change.Fate.MitigatedDamage;
            report.Sent++;
            if (change.Fate.Sunk)
                Logger.Information("[NavalSync] Reported campaign ship {CampaignShipId} of hull {ShipId} sunk", change.Fate.CampaignShipId,
                    change.ShipId);
        }
    }

    // One own hull's fate waiting to go out.
    private readonly struct PendingFate
    {
        public PendingFate(Guid shipId, BattleShipFate fate)
        {
            ShipId = shipId;
            Fate = fate;
        }

        public Guid ShipId { get; }
        public BattleShipFate Fate { get; }
    }

    // The last fate this client sent for one hull, the mitigated damage its fates carried so far and how many it sent.
    private sealed class HullReport
    {
        public BattleShipFate Last;
        public float ReportedMitigatedDamage;
        public int Sent;
    }
}
