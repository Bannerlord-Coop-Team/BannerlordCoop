using Common.Logging;
using GameInterface.Services.MobileParties;
using Serilog;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Entity.Data
{
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
        public Guid OwnerId { get; }

        /// <summary>
        /// Id of the controlled entity.
        /// </summary>
        /// <remarks>
        /// This will normally be the StringId from the <see cref="MBObjectBase"/> class
        /// </remarks>
        public string EntityId { get; }

        public ControlledEntity(Guid ownerId, string entityId)
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
    }
}
