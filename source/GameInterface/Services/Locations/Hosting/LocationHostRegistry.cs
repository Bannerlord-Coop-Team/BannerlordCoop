using GameInterface.Services.Entity;
using System.Collections.Generic;

namespace GameInterface.Services.Locations.Hosting;

/// <inheritdoc cref="ILocationHostRegistry"/>
public class LocationHostRegistry : ILocationHostRegistry
{
    private readonly IControllerIdProvider controllerIdProvider;

    // Written from the network thread (assignment receipt) and the main thread (server election); read from
    // both. A single lock keeps the map consistent.
    private readonly Dictionary<string, LocationHostAssignment> assignments = new();
    private readonly object gate = new();

    public LocationHostRegistry(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public void Set(string instanceId, LocationHostAssignment assignment)
    {
        lock (gate)
        {
            assignments[instanceId] = assignment;
        }
    }

    public bool TryGet(string instanceId, out LocationHostAssignment assignment)
    {
        lock (gate)
        {
            return assignments.TryGetValue(instanceId, out assignment);
        }
    }

    public bool IsHost(string instanceId)
    {
        lock (gate)
        {
            return assignments.TryGetValue(instanceId, out var assignment)
                && assignment.HostControllerId == controllerIdProvider.ControllerId;
        }
    }

    public void Remove(string instanceId)
    {
        lock (gate)
        {
            assignments.Remove(instanceId);
        }
    }
}
