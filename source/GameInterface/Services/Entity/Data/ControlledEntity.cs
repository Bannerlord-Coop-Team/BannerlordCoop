using Common.Logging;
using ProtoBuf;
using Serilog;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Entity.Data;

/// <summary>
/// Controllable entity and owner
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class ControlledEntity
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControlledEntity>();

    /// <summary>
    /// Id of owner of the controlled entity.
    /// This can be either a client or server.
    /// </summary>
    [ProtoMember(1)]
    public string OwnerId { get; }

    /// <summary>
    /// Id of the controlled entity.
    /// </summary>
    /// <remarks>
    /// This will normally be the StringId from the <see cref="MBObjectBase"/> class
    /// </remarks>
    [ProtoMember(2)]
    public string EntityId { get; }

    public ControlledEntity(string ownerId, string entityId)
    {

        if (ownerId == default)
        {
            Logger.Warning("{ownerIdName} was invalid", nameof(ownerId));
        }

        if (string.IsNullOrEmpty(entityId))
        {
            Logger.Warning("{entityIdName} was invalid", nameof(entityId));
        }

        OwnerId = ownerId;
        EntityId = entityId;
    }

    public override bool Equals(object obj)
    {
        if (obj is not ControlledEntity controlledEntity) return false;

        return OwnerId == controlledEntity.OwnerId && EntityId == controlledEntity.EntityId;
    }

    public static bool operator ==(ControlledEntity obj1, ControlledEntity obj2)
    {
        if (ReferenceEquals(obj1, obj2)) return true;
        if (obj1 is null || obj2 is null) return false;
        return obj1.Equals(obj2);
    }

    public static bool operator !=(ControlledEntity obj1, ControlledEntity obj2)
    {
        return !obj1.Equals(obj2);
    }

    public override int GetHashCode()
    {
        int hash = 552523;
        hash = (hash * 31) + OwnerId.GetHashCode();
        hash = (hash * 31) + EntityId.GetHashCode();
        return hash;
    }
}
