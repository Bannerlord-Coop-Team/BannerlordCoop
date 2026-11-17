using GameInterface.Services.Ships.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Server, game thread] The newest reported fate of each campaign ship in a naval battle, held until it is applied to the
/// campaign at a member's departure, at the result commit or when the battle finalizes without one.
/// </summary>
public interface IBattleShipFateLedger
{
    /// <summary>
    /// Holds <paramref name="fate"/> unless the ship's held fate is newer by host epoch, then revision; a sunk ship stays sunk.
    /// Its mitigated damage adds to the ship's total either way.
    /// </summary>
    void Record(string mapEventId, BattleShipFate fate, int hostEpoch, long revision);

    /// <summary>[Game thread] Copies pending fates and captures for server diagnostics without consuming them.</summary>
    object Inspect();

    /// <summary>Whether any fate of the battle waits to be applied.</summary>
    bool HasFates(string mapEventId);

    /// <summary>Removes and returns the battle's held fates that <paramref name="selects"/> picks.</summary>
    IReadOnlyList<BattleShipFate> Take(string mapEventId, Func<BattleShipFate, bool> selects);

    /// <summary>
    /// Records that <paramref name="captorControllerId"/> captured the ship at <paramref name="sequence"/> in its hull's capture
    /// order, so only it reports the ship's fate from now on and its first report supersedes the held one; a null captor (the
    /// owner took it back) ends the record. The first record per place stands and an earlier place changes nothing, so the
    /// result doesn't depend on which report arrives first.
    /// </summary>
    void RecordCapture(string mapEventId, string campaignShipId, string captorControllerId, int sequence);

    /// <summary>The player whose capture of the ship the battle last recorded, or null.</summary>
    string CaptorOf(string mapEventId, string campaignShipId);

    /// <summary>The ships whose last recorded capture in the battle names <paramref name="captorControllerId"/>.</summary>
    IReadOnlyCollection<string> CapturedBy(string mapEventId, string captorControllerId);

    /// <summary>Drops the battle's capture records once it finalized.</summary>
    void ForgetCaptures(string mapEventId);
}

/// <inheritdoc cref="IBattleShipFateLedger"/>
public class BattleShipFateLedger : IBattleShipFateLedger
{
    // Game thread only: the fate handler records inside GameThread.RunSafe and every apply point runs on the game thread.
    private readonly Dictionary<string, Dictionary<string, HeldFate>> battles = new Dictionary<string, Dictionary<string, HeldFate>>();
    // Per battle, the last captor of each ship captured in the mission (null once its owner took it back) and its place.
    private readonly Dictionary<string, Dictionary<string, CaptureRecord>> captures = new Dictionary<string, Dictionary<string, CaptureRecord>>();

    public void Record(string mapEventId, BattleShipFate fate, int hostEpoch, long revision)
    {
        if (!battles.TryGetValue(mapEventId, out var ships))
        {
            ships = new Dictionary<string, HeldFate>();
            battles.Add(mapEventId, ships);
        }

        var candidate = new HeldFate(fate, hostEpoch, revision);
        if (!ships.TryGetValue(fate.CampaignShipId, out var held))
        {
            ships.Add(fate.CampaignShipId, candidate);
            return;
        }

        // Each report carries the damage mitigated since the reporter's last one, so every report adds to the total.
        float mitigatedDamage = held.Fate.MitigatedDamage + fate.MitigatedDamage;
        var kept = candidate.Supersedes(held) ? candidate : held;
        ships[fate.CampaignShipId] = new HeldFate(kept.Fate.WithMitigatedDamage(mitigatedDamage), kept.HostEpoch, kept.Revision);
    }

    // Copies the retained reports, including captures still waiting for their first new-authority report.
    public object Inspect() => battles.Keys.Union(captures.Keys).OrderBy(id => id).Select(instance =>
    {
        battles.TryGetValue(instance, out var fates);
        captures.TryGetValue(instance, out var captured);
        var ids = (fates?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
            .Union(captured?.Keys.AsEnumerable() ?? Enumerable.Empty<string>());
        return new
        {
            instance,
            ships = ids.OrderBy(id => id).Select(id =>
            {
                var held = fates != null && fates.TryGetValue(id, out var fate) ? (HeldFate?)fate : null;
                var capture = captured != null && captured.TryGetValue(id, out var record) ? (CaptureRecord?)record : null;
                bool awaitingCaptureReport = held?.HostEpoch == int.MinValue;
                return new
                {
                    campaignShipId = id,
                    pendingFate = held.HasValue,
                    hitPoints = held?.Fate.HitPoints,
                    sailHitPoints = held?.Fate.SailHitPoints,
                    sunk = held?.Fate.Sunk,
                    mitigatedDamage = held?.Fate.MitigatedDamage,
                    hostEpoch = awaitingCaptureReport ? null : held?.HostEpoch,
                    revision = awaitingCaptureReport ? null : held?.Revision,
                    captor = capture?.Captor,
                    captureSequence = capture?.Sequence,
                    awaitingCaptureReport = held.HasValue ? (bool?)awaitingCaptureReport : null,
                };
            }).ToArray(),
        };
    }).ToArray();

    public bool HasFates(string mapEventId) => battles.TryGetValue(mapEventId, out var ships) && ships.Count > 0;

    public IReadOnlyList<BattleShipFate> Take(string mapEventId, Func<BattleShipFate, bool> selects)
    {
        if (!battles.TryGetValue(mapEventId, out var ships)) return Array.Empty<BattleShipFate>();

        var taken = ships.Values.Select(held => held.Fate).Where(selects).ToList();
        foreach (var fate in taken)
            ships.Remove(fate.CampaignShipId);
        if (ships.Count == 0) battles.Remove(mapEventId);
        return taken;
    }

    public void RecordCapture(string mapEventId, string campaignShipId, string captorControllerId, int sequence)
    {
        if (!captures.TryGetValue(mapEventId, out var ships))
        {
            ships = new Dictionary<string, CaptureRecord>();
            captures.Add(mapEventId, ships);
        }

        if (ships.TryGetValue(campaignShipId, out var recorded) && recorded.Sequence >= sequence) return;

        ships[campaignShipId] = new CaptureRecord(captorControllerId, sequence);
        // The new reporter numbers its fates from its own count, so the held one keeps its condition but no order.
        if (battles.TryGetValue(mapEventId, out var held) && held.TryGetValue(campaignShipId, out var fate))
            held[campaignShipId] = new HeldFate(fate.Fate, int.MinValue, long.MinValue);
    }

    public string CaptorOf(string mapEventId, string campaignShipId) =>
        captures.TryGetValue(mapEventId, out var ships) && ships.TryGetValue(campaignShipId, out var record) ? record.Captor : null;

    public IReadOnlyCollection<string> CapturedBy(string mapEventId, string captorControllerId) =>
        captures.TryGetValue(mapEventId, out var ships)
            ? ships.Where(entry => entry.Value.Captor == captorControllerId).Select(entry => entry.Key).ToList()
            : (IReadOnlyCollection<string>)Array.Empty<string>();

    public void ForgetCaptures(string mapEventId) => captures.Remove(mapEventId);

    // A ship's last recorded captor, null once its owner took it back, and the hull's place in its capture order.
    private readonly struct CaptureRecord
    {
        public CaptureRecord(string captor, int sequence)
        {
            Captor = captor;
            Sequence = sequence;
        }

        public string Captor { get; }
        public int Sequence { get; }
    }

    // One held fate with the stamp it was reported at.
    private readonly struct HeldFate
    {
        public HeldFate(BattleShipFate fate, int hostEpoch, long revision)
        {
            Fate = fate;
            HostEpoch = hostEpoch;
            Revision = revision;
        }

        public BattleShipFate Fate { get; }
        public int HostEpoch { get; }
        public long Revision { get; }

        // Sinking is final in vanilla, so a sunk ship is never raised again and a sink report always lands.
        public bool Supersedes(HeldFate held)
        {
            if (held.Fate.Sunk) return false;
            if (Fate.Sunk) return true;
            return HostEpoch != held.HostEpoch ? HostEpoch > held.HostEpoch : Revision > held.Revision;
        }
    }
}
