using GameInterface.Registry;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ObjectManager;

public interface IObjectManager
{
    /// <summary>
    /// Determines if an object is stored in the object manager
    /// </summary>
    /// <param name="obj">Object to check if stored</param>
    /// <returns>True if stored, false if not</returns>
    bool Contains(object obj);

    /// <summary>
    /// Determines if a StringId is stored in the object manager
    /// </summary>
    /// <param name="id">StringId to check if stored</param>
    /// <returns>True if stored, false if not</returns>
    bool Contains(string id);

    /// <summary>
    /// Attempts to get an object's StringId from the object itself
    /// </summary>
    /// <param name="obj">Object to get StringId from</param>
    /// <param name="id">Out parameter for string id (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetId(object obj, out string id);
    bool TryGetIdWithLogging(object obj, out string id);

    /// <summary>
    /// Attempts to get an object using a StringId and object type
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>
    /// <param name="id">StringId used to lookup object</param>
    /// <param name="obj">Out parameter for the object (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetObject<T>(string id, out T obj);
    bool TryGetObjectWithLogging<T>(string id, out T obj);

    /// <summary>
    /// Add an object with already existing StringId
    /// </summary>
    /// <param name="id">Id to assosiate with object</param>
    /// <param name="obj">Object to assosiate with id</param>
    /// <returns>True if successful, false if failed</returns>
    bool AddExisting(string id, object obj);

    /// <summary>
    /// Adds an object without a registered StringId
    /// </summary>
    /// <param name="obj">Object to register</param>
    /// <param name="newId">Newly created StringId</param>
    /// <returns>True if successful, false if failed</returns>
    bool AddNewObject(object obj, out string newId);

    /// <summary>
    /// Removes an object from the <see cref="IObjectManager"/>
    /// </summary>
    /// <param name="obj">Object to remove</param>
    /// <returns>True if successful, false if failed</returns>
    bool Remove(object obj);


    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <remarks>After calling this method, the collection will be empty. This method does not throw an
    /// exception if the collection is already empty.</remarks>
    void Clear();

    /// <summary>
    /// Generates a new unique id for the given object using the specified base string.
    /// </summary>
    /// <param name="obj">Object to generate a new id for</param>
    /// <param name="baseId">Base string to use for id generation (e.g. "Hero_looters1_1"). The final id will be in the format "{typeName}_{baseId}_{N}" where N is a unique number.</param>
    /// <returns>Newly generated unique id</returns>
    string CreateNewId(object obj, string baseId);

    int GetUniqueTypeId(object obj);
    int EnsureNextUniqueIdAbove(object obj, int value);
}

/// <summary>
/// Ground truth for storing and retrieving object and ids
/// </summary>
public class ObjectManager : IObjectManager
{
    private readonly ILogger logger;

    protected readonly ConcurrentDictionary<string, object> idObjs = new ConcurrentDictionary<string, object>();
    protected ConditionalWeakTable<object, string> objsIds = new ConditionalWeakTable<object, string>();

    private readonly ConcurrentDictionary<Type, int> objectCounters = new ConcurrentDictionary<Type, int>();

    private readonly object _gate = new();

    public ObjectManager(ILogger logger)
    {
        this.logger = logger;
    }

    public bool AddExisting(string id, object obj)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (obj == null) return false;

        lock (_gate)
        {
            // Skip to next id
            GetUniqueTypeId(obj);

            if (objsIds.TryGetValue(obj, out var _))
            {
                logger.Error("Object already registered: {ObjectType}", obj.GetType());
                return false;
            }

            if (!idObjs.TryAdd(id, obj))
            {
                logger.Error("Duplicate id: {id}", id);
                return false;
            }

            objsIds.Add(obj, id);

            return true;
        }
    }

    public bool AddNewObject(object obj, out string newId)
    {
        newId = null;
        if (obj == null) return false;

        lock (_gate)
        {
            if (objsIds.TryGetValue(obj, out var existingId))
            {
                logger.Warning("Object already registered with id {Id}: {ObjectType}", existingId, obj.GetType());
                return false;
            }

            newId = $"{obj.GetType().Name}_{GetUniqueTypeId(obj)}";

            if (!idObjs.TryAdd(newId, obj))
            {
                logger.Error(
                    "Generated duplicate id {Id} for object type {ObjectType}",
                    newId,
                    obj.GetType());

                return false;
            }

            objsIds.Add(obj, newId);

            return true;
        }
    }

    public string CreateNewId(object obj, string baseId)
    {
        return $"{obj.GetType().Name}_{baseId}";
    }

    public int GetUniqueTypeId(object obj)
    {
        var type = obj.GetType();

        lock(objectCounters)
        {
            return objectCounters.AddOrUpdate(
                type,
                1,                // initial value if missing
                (_, current) => current + 1
            );
        }
    }

    public int EnsureNextUniqueIdAbove(object obj, int value)
    {
        var type = obj.GetType();

        lock (objectCounters)
        {
            int nextValue = value + 1;

            return objectCounters.AddOrUpdate(
                type,
                nextValue,
                (_, current) => nextValue > current ? nextValue : current
            );
        }
    }

    public bool Contains(object obj)
    {
        if (obj == null) return false;

        return objsIds.TryGetValue(obj, out var _);
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        return idObjs.ContainsKey(id);
    }

    public bool TryGetId(object obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        return objsIds.TryGetValue(obj, out id);
    }
    public bool TryGetObject<T>(string id, out T obj)
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        if (!idObjs.TryGetValue(id, out var storedObj)
            && !idObjs.TryGetValue($"{typeof(T).Name}_{id}", out storedObj)) // If object not found also attempt with prefixed type name
        {
            return false;
        }

        if (storedObj is not T castedObject)
        {
            logger.Error("Could not cast ({ActualType}) object to type {ObjectType}", storedObj.GetType(), typeof(T));
            return false;
        }

        obj = castedObject;

        return true;
    }

    public bool Remove(object obj)
    {
        if (obj == null) return false;

        lock (_gate)
        {
            if (objsIds.TryGetValue(obj, out var id) == false) return false;

            return idObjs.TryRemove(id, out _) && objsIds.Remove(obj);
        }
    }

    #region LogHelpers
    public bool TryGetIdWithLogging(object obj, out string id)
    {
        id = null;

        if (obj == null)
        {
            logger.Error(
                "[{ClassName}] Failed to get id because object was null",
                nameof(ObjectManager));

            return false;
        }


        if (!TryGetId(obj, out id))
        {
            if (obj is MBObjectBase mbObject)
            {
                logger.Error(
                    "[{ClassName}] Failed to get id for object of type {ObjectType}, {StringId}",
                    nameof(ObjectManager),
                    obj.GetType().FullName,
                    mbObject.StringId);
                return false;
            }

            logger.Error(
                "[{ClassName}] Failed to get id for object of type {ObjectType}",
                nameof(ObjectManager),
                obj.GetType().FullName);

            return false;
        }

        return true;
    }

    public bool TryGetObjectWithLogging<T>(string id, out T obj)
    {
        obj = default;

        if (id == null)
        {
            logger.Error(
                "[{ClassName}] Failed to get object because id was null",
                nameof(ObjectManager));

            return false;
        }

        if (!TryGetObject(id, out obj))
        {
            logger.Error(
                "[{ClassName}] Failed to get {name} using {id}",
                nameof(ObjectManager),
                typeof(T),
                id
            );

            return false;
        }

        return true;
    }

    public void Clear()
    {
        lock (_gate)
        {
            objsIds = new ConditionalWeakTable<object, string>();
            idObjs.Clear();
        }
    }
    #endregion
}