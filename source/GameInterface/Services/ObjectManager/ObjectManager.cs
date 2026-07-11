using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    bool TryGetIdWithLogging<T>(T obj, out string id);

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
    /// Atomically registers a batch of existing objects. Returns false without changing either lookup map
    /// when the input is null, contains an empty id or null object, repeats an id or object reference, collides
    /// with an existing registration, cannot be enumerated, or cannot be fully committed.
    /// </summary>
    bool AddExistingBatch(IEnumerable<KeyValuePair<string, object>> objects);

    /// <summary>
    /// Removes every currently registered object in one registry critical section. Objects which are
    /// already absent are ignored, allowing aggregate teardown to coexist with ordinary child destroy hooks.
    /// </summary>
    bool RemoveExistingBatch(IEnumerable<object> objects);

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

    public bool AddExistingBatch(IEnumerable<KeyValuePair<string, object>> objects)
    {
        if (objects == null) return false;

        List<KeyValuePair<string, object>> batch;
        try
        {
            batch = new List<KeyValuePair<string, object>>(objects);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unable to enumerate object registration batch");
            return false;
        }

        lock (_gate)
        {
            var batchIds = new HashSet<string>(StringComparer.Ordinal);
            var batchObjects = new HashSet<object>(ReferenceObjectComparer.Instance);

            foreach (var pair in batch)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    logger.Error("Unable to register object batch because an id was null or empty");
                    return false;
                }

                if (pair.Value == null)
                {
                    logger.Error("Unable to register object batch because object {Id} was null", pair.Key);
                    return false;
                }

                if (!batchIds.Add(pair.Key))
                {
                    logger.Error("Unable to register object batch because id {Id} was repeated", pair.Key);
                    return false;
                }

                if (!batchObjects.Add(pair.Value))
                {
                    logger.Error(
                        "Unable to register object batch because object {ObjectType} was repeated",
                        pair.Value.GetType());
                    return false;
                }

                if (idObjs.ContainsKey(pair.Key))
                {
                    logger.Error("Unable to register object batch because id {Id} already exists", pair.Key);
                    return false;
                }

                if (objsIds.TryGetValue(pair.Value, out var existingId))
                {
                    logger.Error(
                        "Unable to register object batch because object {ObjectType} is already registered as {Id}",
                        pair.Value.GetType(),
                        existingId);
                    return false;
                }
            }

            var committed = new List<KeyValuePair<string, object>>(batch.Count);
            try
            {
                foreach (var pair in batch)
                {
                    if (!idObjs.TryAdd(pair.Key, pair.Value))
                    {
                        throw new InvalidOperationException($"Duplicate id encountered while committing batch: {pair.Key}");
                    }

                    try
                    {
                        objsIds.Add(pair.Value, pair.Key);
                    }
                    catch
                    {
                        idObjs.TryRemove(pair.Key, out _);
                        throw;
                    }

                    committed.Add(pair);
                }
            }
            catch (Exception ex)
            {
                for (int i = committed.Count - 1; i >= 0; i--)
                {
                    var pair = committed[i];
                    idObjs.TryRemove(pair.Key, out _);
                    objsIds.Remove(pair.Value);
                }

                logger.Error(ex, "Unable to atomically commit object registration batch");
                return false;
            }

            // Match AddExisting's counter advancement only after every mapping has committed. Counter state is
            // intentionally not part of the registration transaction; it cannot make a committed id/object map
            // partially visible.
            foreach (var pair in batch)
            {
                GetUniqueTypeId(pair.Value);
            }

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

    public bool RemoveExistingBatch(IEnumerable<object> objects)
    {
        if (objects == null) return false;

        List<object> batch;
        try
        {
            batch = new List<object>(objects);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unable to enumerate object removal batch");
            return false;
        }

        lock (_gate)
        {
            var uniqueObjects = new HashSet<object>(ReferenceObjectComparer.Instance);
            foreach (var obj in batch)
            {
                if (obj == null || !uniqueObjects.Add(obj)) continue;
                if (!objsIds.TryGetValue(obj, out var id)) continue;

                idObjs.TryRemove(id, out _);
                objsIds.Remove(obj);
            }

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

        lock (_gate)
        {
            return objsIds.TryGetValue(obj, out var _);
        }
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        lock (_gate)
        {
            return idObjs.ContainsKey(id);
        }
    }

    public bool TryGetId(object obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        lock (_gate)
        {
            return objsIds.TryGetValue(obj, out id);
        }
    }
    public bool TryGetObject<T>(string id, out T obj)
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        lock (_gate)
        {
            // Compacted ids arrive without their "{TypeName}_" prefix, so try the prefixed key first;
            // a bare id could otherwise collide with an un-prefixed key registered for another object.
            if (!idObjs.TryGetValue($"{typeof(T).Name}_{id}", out var storedObj)
                && !idObjs.TryGetValue(id, out storedObj))
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
    }

    /// <summary>
    /// Strips the redundant leading "{type.Name}_" prefix from a registered id for the wire; the
    /// receiver re-adds it by type in <see cref="TryGetObject{T}"/>. Conditional, so a full id whose
    /// concrete type differs from the wire type is transmitted untouched.
    /// </summary>
    public static string Compact(string id, Type type)
    {
        if (string.IsNullOrEmpty(id) || type == null) return id;

        var prefix = type.Name + "_";
        return id.StartsWith(prefix, StringComparison.Ordinal)
            ? id.Substring(prefix.Length)
            : id;
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
    public bool TryGetIdWithLogging<T>(T obj, out string id)
    {
        id = null;

        if (obj == null)
        {
            logger.Error(
                "[{ClassName}] Failed to get id because object was null ({ObjectType})",
                nameof(ObjectManager),
                typeof(T));

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

    private sealed class ReferenceObjectComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceObjectComparer Instance = new ReferenceObjectComparer();

        private ReferenceObjectComparer()
        {
        }

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
