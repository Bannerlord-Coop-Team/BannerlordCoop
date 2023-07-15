using Common.Logging;
using Serilog;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Entity.Data;

/// <summary>
/// Controllable entity and owner
/// </summary>
public class ControlledEntity
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControlledEntity>();

    /// <summary>
    /// Id of owner of the controlled entity.
    /// This can be either a client or server.
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// Id of the controlled entity.
    /// </summary>
    /// <remarks>
    /// This will normally be the StringId from the <see cref="MBObjectBase"/> class
    /// </remarks>
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

    public override int GetHashCode() => base.GetHashCode();
}
