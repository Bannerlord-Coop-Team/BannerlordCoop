using Common.Logging;
using Common.Messaging;
using Common.Network.Instances.Messages;
using Serilog;

namespace Common.Network.Instances;

/// <summary>
/// Process-wide view of the P2P instance the local player currently belongs to. Bridges the two
/// DI containers (campaign <c>Coop.Core</c> and mission <c>Missions</c>) which share the single
/// <see cref="MessageBroker.Instance"/>: the campaign side publishes the instance assignment, and
/// the mission side reads <see cref="CurrentInstanceId"/> / <see cref="IsHost"/> here.
/// </summary>
public interface IInstanceContext
{
    /// <summary>Server-issued id of the current instance, or null when not in an instance.</summary>
    string CurrentInstanceId { get; }

    /// <summary>True when this client owns NPC simulation for the current instance.</summary>
    bool IsHost { get; }

    /// <summary>True while assigned to an instance.</summary>
    bool InInstance { get; }
}

/// <inheritdoc cref="IInstanceContext"/>
public class InstanceContext : IInstanceContext
{
    private static readonly ILogger Logger = LogManager.GetLogger<InstanceContext>();

    // Rooted singleton so the weak-referenced broker subscriptions are never collected.
    public static InstanceContext Instance { get; } = new InstanceContext();

    public string CurrentInstanceId { get; private set; }
    public bool IsHost { get; private set; }
    public bool InInstance => string.IsNullOrEmpty(CurrentInstanceId) == false;

    private InstanceContext()
    {
        MessageBroker.Instance.Subscribe<InstanceAssigned>(Handle_Assigned);
        MessageBroker.Instance.Subscribe<InstanceHostChanged>(Handle_HostChanged);
        MessageBroker.Instance.Subscribe<InstanceCleared>(Handle_Cleared);
    }

    private void Handle_Assigned(MessagePayload<InstanceAssigned> payload)
    {
        CurrentInstanceId = payload.What.InstanceId;
        IsHost = payload.What.IsHost;
        Logger.Information("Instance context set to {InstanceId} (host={IsHost})", CurrentInstanceId, IsHost);
    }

    private void Handle_HostChanged(MessagePayload<InstanceHostChanged> payload)
    {
        if (payload.What.InstanceId != CurrentInstanceId) return;
        IsHost = payload.What.IsHost;
        Logger.Information("Instance {InstanceId} host changed to {IsHost}", CurrentInstanceId, IsHost);
    }

    private void Handle_Cleared(MessagePayload<InstanceCleared> payload)
    {
        CurrentInstanceId = null;
        IsHost = false;
        Logger.Debug("Instance context cleared");
    }
}
