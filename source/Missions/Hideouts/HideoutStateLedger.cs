using System;
using System.Collections.Generic;

namespace Missions.Hideouts;

internal sealed class HideoutStateLedger
{
    private readonly HashSet<int> spawned = new();
    public NetworkHideoutState Latest { get; private set; }

    public void RecordSpawn(int seed) => spawned.Add(seed);
    public bool WasSpawned(int seed) => spawned.Contains(seed);
    public int[] GetSpawnedSeeds()
    {
        var seeds = new int[spawned.Count];
        spawned.CopyTo(seeds);
        Array.Sort(seeds);
        return seeds;
    }

    public bool TryAccept(NetworkHideoutState state, string instanceId, string hostControllerId, int hostEpoch)
    {
        if (state == null || state.InstanceId != instanceId || state.HostControllerId != hostControllerId ||
            state.HostEpoch != hostEpoch || hostEpoch <= 0 || state.Revision <= 0)
            return false;
        if (Latest != null && (state.HostEpoch < Latest.HostEpoch ||
            state.HostEpoch == Latest.HostEpoch && state.Revision <= Latest.Revision))
            return false;

        Latest = state;
        if (state.SpawnedSeeds != null)
            spawned.UnionWith(state.SpawnedSeeds);
        return true;
    }
}
