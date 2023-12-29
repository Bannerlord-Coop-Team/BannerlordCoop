using Common;
using Common.Logging;
using GameInterface.Services.Entity.Data;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaleWorlds.Core;

namespace GameInterface.Services.Entity;

/// <summary>
/// Stores ownership of game entities
/// </summary>
/// <remarks>A game entity is anything that can be updated by the client or server</remarks>
public interface IControlledEntityRegistry
{
    /// <summary>
    /// Packages an immutable dictionary of controlled entities
    /// </summary>
    /// <returns>Immutable dictionary of controlled entities</returns>
    IReadOnlyDictionary<string, IReadOnlySet<ControlledEntity>> PackageControlledEntities();

    /// <summary>
    /// Registers an Enumerable of entities with the registry
    /// </summary>
    /// <param name="entityIds">Entities to register, normally retrieved from a save file</param>
    void RegisterExistingEntities(IEnumerable<ControlledEntity> entityIds);

    /// <summary>
    /// Determines if the given entity string id is owned by the calling server or client
    /// </summary>
    /// <param name="controllerId">Owner to check if entity is owned by</param>
    /// <param name="entityId">Entity to check ownership</param>
    /// <returns>True if entity <see cref="OwnershipId"/> matches entity controller, otherwise False</returns>
    bool IsControlledBy(string controllerId, string entityId);

    /// <summary>
    /// Registers a controlled relationship between the owner and entity
    /// </summary>
    /// <param name="controllerId">Id of owner</param>
    /// <param name="entityId">Id of entity</param>
    /// <remarks>Normally the StringId from <see cref="MBObjectBase"/></remarks>
    /// <returns>True if registration was successful, otherwise False</returns>
    bool RegisterAsControlled(string controllerId, string entityId);

    /// <summary>
    /// Registers a controlled relationship between the owner and entity
    /// </summary>
    /// <param name="controllerId">Id of owner</param>
    /// <param name="entityId">Id of entity</param>
    /// <param name="newEntity">Newly created controlled entity</param>
    /// <remarks>Normally the StringId from <see cref="MBObjectBase"/></remarks>
    /// <returns>True if registration was successful, otherwise False</returns>
    bool RegisterAsControlled(string controllerId, string entityId, out ControlledEntity newEntity);

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
    /// <summary>
    /// Attempts to retrieve all controlled entities using the given controller Id
    /// </summary>
    /// <param name="controllerId">Controller to find entities for</param>
    /// <param name="controlledEntities">Enumerable of controlled entities</param>
    /// <returns>True if the controller has controlled entities, otherwise False</returns>
    bool TryGetControlledEntities(string controllerId, out IEnumerable<ControlledEntity> controlledEntities);
}

[ProtoContract]
internal class ControlledEntityRegistry : IControlledEntityRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControlledEntityRegistry>();

    [ProtoMember(1)]
    private readonly ConcurrentDictionary<string, HashSet<ControlledEntity>> controlledEntities = new ConcurrentDictionary<string, HashSet<ControlledEntity>>();
    [ProtoMember(2)]
    private readonly ConcurrentDictionary<string, ControlledEntity> controllerIdLookup = new ConcurrentDictionary<string, ControlledEntity>();

    public IReadOnlyDictionary<string, IReadOnlySet<ControlledEntity>> PackageControlledEntities()
    {
        // Make dictionary immutable
        var readonlyListDict = controlledEntities.ToDictionary(k => k.Key, k => k.Value.AsReadOnly() as IReadOnlySet<ControlledEntity>);

        return new ReadOnlyDictionary<string, IReadOnlySet<ControlledEntity>>(readonlyListDict);
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

    public bool IsControlledBy(string ownerId, string entityId)
    {
        if(controllerIdLookup.TryGetValue(entityId, out var entity) == false) return false;

        return entity.OwnerId == ownerId;
    }

    public bool RegisterAsControlled(string ownerId, string entityId) => RegisterAsControlled(ownerId, entityId, out var _);
    public bool RegisterAsControlled(string ownerId, string entityId, out ControlledEntity newEntity)
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

    public bool TryGetControlledEntities(string controllerId, out IEnumerable<ControlledEntity> controlledEntities)
    {
        var result = this.controlledEntities.TryGetValue(controllerId, out var controlledEntitiesHashset);

        controlledEntities = controlledEntitiesHashset;

        if (result == false) return false;
        
        if (controlledEntitiesHashset.IsEmpty()) return false;

        return true;
    }

    public override bool Equals(object obj)
    {
        if (obj is not ControlledEntityRegistry entityRegistry) return false;

        if (controlledEntities.Count != entityRegistry.controlledEntities.Count) return false;

        foreach(var key in controlledEntities.Keys)
        {
            if (!entityRegistry.controlledEntities.ContainsKey(key)) return false;

            if (controlledEntities[key].SequenceEqual(entityRegistry.controlledEntities[key]) == false) return false;
        }

        if (controllerIdLookup.Count != entityRegistry.controllerIdLookup.Count) return false;

        foreach (var key in controllerIdLookup.Keys)
        {
            if (!entityRegistry.controllerIdLookup.ContainsKey(key)) return false;

            if (controllerIdLookup[key].Equals(entityRegistry.controllerIdLookup[key]) == false) return false;
        }

        return true;
    }

    public override int GetHashCode() => base.GetHashCode();
}
