#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Messages;

namespace Missions.Battles;

public interface INavalLabNativeState
{
    void Initialize(NavalLabManifest manifest);
    bool Ready { get; }
    bool Deploy(string owner);
    bool Offer(string owner, NetworkNavalLabStations stations);
    bool Acknowledge(string owner, NetworkNavalLabStations stations);
    void Release(string owner, NetworkNavalLabStations release);
    NetworkNavalLabStations[] Stations { get; }
    void Stop();
}

public sealed class NavalLabNativeState : INavalLabNativeState
{
    private NavalLabManifest manifest;
    private readonly HashSet<string> deployed = new();
    private readonly Dictionary<int, NetworkNavalLabStations> stations = new();
    private readonly HashSet<string> acknowledgements = new();
    private readonly NetworkNavalLabStations[] releases = new NetworkNavalLabStations[2];
    private bool stopped;
    public bool Ready => !stopped && deployed.Count == 2 && stations.Count == 2 && acknowledgements.Count == 4;
    public NetworkNavalLabStations[] Stations => stations.Values.OrderBy(item => item.Ship).ToArray();

    public void Initialize(NavalLabManifest value)
    {
        if (manifest != null || value == null) throw new InvalidOperationException("native.invalid_session");
        manifest = value;
    }

    public bool Deploy(string owner)
    {
        CheckOwner(owner);
        deployed.Add(owner);
        return deployed.Count == 2;
    }

    private void CheckOwner(string owner)
    {
        if (stopped || manifest == null || !manifest.Controllers.Contains(owner)) throw new InvalidOperationException("native.invalid_owner_or_terminal");
    }

    private void CheckStations(NetworkNavalLabStations value)
    {
        if (value == null || value.IncarnationId != manifest.IncarnationId || value.Epoch != 1 || value.Ship < 0 || value.Ship >= 2
            || value.Combatants == null || value.Keys == null || value.Combatants.Length != 4 || value.Keys.Length != 4
            || value.Combatants.Distinct().Count() != 4 || value.Keys.Distinct().Count() != 4
            || value.Keys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > 256)
            || !value.Combatants.SequenceEqual(manifest.Combatants.Skip((value.Ship * 5) + 1).Take(4)))
            throw new InvalidOperationException("native.invalid_station_manifest");
    }

    private static bool Same(NetworkNavalLabStations a, NetworkNavalLabStations b) =>
        a.Keys.SequenceEqual(b.Keys) && a.Combatants.SequenceEqual(b.Combatants);

    public bool Offer(string owner, NetworkNavalLabStations value)
    {
        CheckOwner(owner);
        CheckStations(value);
        if (manifest.Controllers[value.Ship] != owner || !deployed.Contains(owner)) throw new InvalidOperationException("native.station_owner_not_deployed");
        if (stations.TryGetValue(value.Ship, out var prior))
        {
            if (!Same(prior, value)) throw new InvalidOperationException("native.conflicting_stations");
            return false;
        }
        stations.Add(value.Ship, value);
        return true;
    }

    public bool Acknowledge(string owner, NetworkNavalLabStations value)
    {
        CheckOwner(owner);
        CheckStations(value);
        if (deployed.Count != 2 || stations.Count != 2 || !stations.TryGetValue(value.Ship, out var prior) || !Same(prior, value))
            throw new InvalidOperationException("native.early_or_mismatched_ack");
        return acknowledgements.Add(owner + ":" + value.Ship);
    }

    // Only the original owner releases its committed stations, one ordered revision at a time and never re-occupied.
    public void Release(string owner, NetworkNavalLabStations value)
    {
        CheckOwner(owner);
        CheckStations(value);
        if (!Ready || value.Phase != "release" || manifest.Controllers[value.Ship] != owner
            || !stations.TryGetValue(value.Ship, out var committed) || !Same(committed, value))
            throw new InvalidOperationException("native.station_release_identity");
        var prior = releases[value.Ship];
        if (value.Released == null || value.Released.Length != 4 || value.Revision != (prior?.Revision ?? 0) + 1
            || Enumerable.Range(0, 4).Any(crew => prior?.Released[crew] == true && !value.Released[crew])
            || Enumerable.Range(0, 4).All(crew => value.Released[crew] == (prior?.Released[crew] ?? false)))
            throw new InvalidOperationException("native.station_release_order");
        releases[value.Ship] = value;
    }

    public void Stop() => stopped = true;
}
#endif
