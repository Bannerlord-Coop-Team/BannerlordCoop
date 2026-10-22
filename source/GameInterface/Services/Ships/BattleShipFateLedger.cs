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

    /// <summary>Whether any fate of the battle waits to be applied.</summary>
    bool HasFates(string mapEventId);

    /// <summary>Removes and returns the battle's held fates that <paramref name="selects"/> picks.</summary>
    IReadOnlyList<BattleShipFate> Take(string mapEventId, Func<BattleShipFate, bool> selects);

    /// <summary>
    /// Records that <paramref name="captorControllerId"/> captured the ship in the mission, so only it reports the ship's fate from
    /// now on and its first report supersedes the held one; a null captor (the owner took it back) clears the record.
    /// </summary>
    void RecordCapture(string mapEventId, string campaignShipId, string captorControllerId);

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
    // Per battle, the captor of each ship captured in the mission.
    private readonly Dictionary<string, Dictionary<string, string>> captures = new Dictionary<string, Dictionary<string, string>>();

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

    public void RecordCapture(string mapEventId, string campaignShipId, string captorControllerId)
    {
        if (!captures.TryGetValue(mapEventId, out var ships))
        {
            ships = new Dictionary<string, string>();
            captures.Add(mapEventId, ships);
        }

        if (captorControllerId == null) ships.Remove(campaignShipId);
        else ships[campaignShipId] = captorControllerId;

        // The new reporter numbers its fates from its own count, so the held one keeps its condition but no order.
        if (battles.TryGetValue(mapEventId, out var held) && held.TryGetValue(campaignShipId, out var fate))
            held[campaignShipId] = new HeldFate(fate.Fate, int.MinValue, long.MinValue);
    }

    public string CaptorOf(string mapEventId, string campaignShipId) =>
        captures.TryGetValue(mapEventId, out var ships) && ships.TryGetValue(campaignShipId, out var captor) ? captor : null;

    public IReadOnlyCollection<string> CapturedBy(string mapEventId, string captorControllerId) =>
        captures.TryGetValue(mapEventId, out var ships)
            ? ships.Where(entry => entry.Value == captorControllerId).Select(entry => entry.Key).ToList()
            : (IReadOnlyCollection<string>)Array.Empty<string>();

    public void ForgetCaptures(string mapEventId) => captures.Remove(mapEventId);

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
