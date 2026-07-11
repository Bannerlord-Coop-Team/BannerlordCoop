using GameInterface.Services.Entity;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <inheritdoc cref="IBattleHostRegistry"/>
public class BattleHostRegistry : IBattleHostRegistry
{
    private readonly IControllerIdProvider controllerIdProvider;

    // Written from the network thread (assignment receipt) and the main thread (server election); read from
    // both. A single lock keeps the map consistent.
    private readonly Dictionary<string, BattleHostAssignment> assignments = new();
    private readonly object gate = new();

    public BattleHostRegistry(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public void Set(string mapEventId, BattleHostAssignment assignment)
    {
        lock (gate)
        {
            assignments[mapEventId] = assignment;
        }
    }

    public bool TryGet(string mapEventId, out BattleHostAssignment assignment)
    {
        lock (gate)
        {
            return assignments.TryGetValue(mapEventId, out assignment);
        }
    }

    public bool IsHost(string mapEventId)
    {
        lock (gate)
        {
            return assignments.TryGetValue(mapEventId, out var assignment)
                && assignment.HostControllerId == controllerIdProvider.ControllerId;
        }
    }

    public void Remove(string mapEventId)
    {
        lock (gate)
        {
            assignments.Remove(mapEventId);
        }
    }
}
