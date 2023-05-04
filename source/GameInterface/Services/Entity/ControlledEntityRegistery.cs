using Common;
using Common.Logging;
using GameInterface.Services.Entity.Data;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameInterface.Services.Entity
{
    /// <summary>
    /// Stores ownership of game entities
    /// </summary>
    /// <remarks>A game entity is anything that can be updated by the client or server</remarks>
    internal interface IControlledEntityRegistery
    {
        /// <summary>
        /// Owner id of the current client or server that instantiated this object.
        /// </summary>
        Guid InstanceOwnerId { get; set; }

        /// <summary>
        /// Packages an immutable dictionary of controlled entities
        /// </summary>
        /// <returns>Immutable dictionary of controlled entities</returns>
        IReadOnlyDictionary<Guid, IReadOnlySet<ControlledEntity>> PackageControlledEntities();

        /// <summary>
        /// Registers an Enumerable of entities with the registry
        /// </summary>
        /// <param name="entityIds">Entities to register, normally retrieved from a save file</param>
        void RegisterExistingEntities(IEnumerable<ControlledEntity> entityIds);

        /// <summary>
        /// Determines if the given entity string id is owned by the calling server or client
        /// </summary>
        /// <param name="entityId">Entity to check ownership</param>
        /// <returns>True if entity <see cref="OwnershipId"/> matches entity controller, otherwise False</returns>
        bool IsOwned(string entityId);

        /// <summary>
        /// Registers a controlled relationship between the owner and entity
        /// </summary>
        /// <param name="ownerId">Id of owner</param>
        /// <param name="entityId">Id of entity</param>
        /// <remarks>Normally the StringId from <see cref="MBObjectBase"/></remarks>
        /// <returns>True if registration was successful, otherwise False</returns>
        bool RegisterAsControlled(Guid ownerId, string entityId);

        /// <summary>
        /// Registers a controlled relationship between the owner and entity
        /// </summary>
        /// <param name="ownerId">Id of owner</param>
        /// <param name="entityId">Id of entity</param>
        /// <param name="newEntity">Newly created controlled entity</param>
        /// <remarks>Normally the StringId from <see cref="MBObjectBase"/></remarks>
        /// <returns>True if registration was successful, otherwise False</returns>
        bool RegisterAsControlled(Guid ownerId, string entityId, out ControlledEntity newEntity);

        /// <summary>
        /// Removes entity as controlled
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        bool RemoveAsControlled(ControlledEntity entityId);
        /// <summary>
        /// Converts the entity id to the controlled entity.
        /// </summary>
        /// <param name="entityId">Entity to resolve</param>
        /// <param name="entity">Resolved entity</param>
        /// <returns>True if the entity is registered, otherwise False</returns>
        bool TryGetControlledEntity(string entityId, out ControlledEntity entity);
    }

    internal class ControlledEntityRegistery : IControlledEntityRegistery
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ControlledEntityRegistery>();

        public Guid InstanceOwnerId { get; set; }

        private ConcurrentDictionary<Guid, HashSet<ControlledEntity>> controlledEntities = new ConcurrentDictionary<Guid, HashSet<ControlledEntity>>();

        private ConcurrentDictionary<string, ControlledEntity> controllerIdLookup = new ConcurrentDictionary<string, ControlledEntity>();

        public IReadOnlyDictionary<Guid, IReadOnlySet<ControlledEntity>> PackageControlledEntities()
        {
            // Make dictionary immutable
            var readonlyListDict = controlledEntities.ToDictionary(k => k.Key, k => k.Value.AsReadOnly() as IReadOnlySet<ControlledEntity>);

            return new ReadOnlyDictionary<Guid, IReadOnlySet<ControlledEntity>>(readonlyListDict);
        }

        public void RegisterExistingEntities(IEnumerable<ControlledEntity> entityIds)
        {
            foreach(var entity in entityIds)
            {
                if (RegisterAsControlled(entity.OwnerId, entity.EntityId) == false)
                {
                    Logger.Warning("Unable to register {entity}", entity);
                }
            }
        }

        public bool IsOwned(string entityId)
        {
            if(controllerIdLookup.TryGetValue(entityId, out var entity) == false) return false;

            return entity.OwnerId == InstanceOwnerId;
        }

        public bool RegisterAsControlled(Guid ownerId, string entityId) => RegisterAsControlled(ownerId, entityId, out var _);
        public bool RegisterAsControlled(Guid ownerId, string entityId, out ControlledEntity newEntity)
        {
            newEntity = null;

            if (controllerIdLookup.ContainsKey(entityId)) return false;

            var result = true;
            newEntity = new ControlledEntity(ownerId, entityId);
            if (controlledEntities.TryGetValue(ownerId, out var entities))
            {
                result &= entities.Add(newEntity);
            }
            else
            {
                result &= controlledEntities.TryAdd(ownerId, new HashSet<ControlledEntity> { newEntity });
            }

            if (result && controllerIdLookup.TryAdd(entityId, newEntity) == false)
            {
                Logger.Error(
                        "{newEntity} was added to {controlledEntities}, " +
                        "but could not be added to {idLookup}",
                        newEntity,
                        nameof(controlledEntities),
                        nameof(controllerIdLookup));

                // Attempt removal of invalid entity to remedy
                RemoveAsControlled(newEntity);

                return false;
            }

            return result;
        }

        public bool RemoveAsControlled(ControlledEntity entityId)
        {
            var result = controlledEntities.TryRemove(entityId.OwnerId, out var _);
            result &= controllerIdLookup.TryRemove(entityId.EntityId, out _);

            return result;
        }

        public bool TryGetControlledEntity(string entityId, out ControlledEntity entity) => controllerIdLookup.TryGetValue(entityId, out entity);
    }
}
