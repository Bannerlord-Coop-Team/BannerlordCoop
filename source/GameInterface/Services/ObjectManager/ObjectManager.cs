using GameInterface.Registry;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

    namespace GameInterface.Services.ObjectManager;

public interface IObjectManager
{
    /// <summary>
    /// Determins if an object is stored in the object manager
    /// </summary>
    /// <param name="obj">Object to check if stored</param>
    /// <returns>True if stored, false if not</returns>
    bool Contains(object obj);

    /// <summary>
    /// Determins if an StringId is stored in the object manager
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
}

/// <summary>
/// Ground truth for storing and retreiving object and ids
/// </summary>
public class ObjectManager : IObjectManager
{
    private readonly ILogger logger;

    protected readonly Dictionary<string, object> idObjs = new Dictionary<string, object>();
    protected ConditionalWeakTable<object, string> objsIds = new ConditionalWeakTable<object, string>();

    private readonly ConcurrentDictionary<Type, int> objectCounters = new ConcurrentDictionary<Type, int>();

    private readonly object _gate = new();

    public ObjectManager(ILogger logger)
    {
        /// <summary>
        /// Determins if an object is stored in the object manager
        /// </summary>
        /// <param name="obj">Object to check if stored</param>
        /// <returns>True if stored, false if not</returns>
        bool Contains(object obj);

        /// <summary>
        /// Determins if an StringId is stored in the object manager
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
    }

    /// <summary>
    /// Ground truth for storing and retreiving object and ids
    /// </summary>
    public class ObjectManager : IObjectManager
    {
        private readonly ILogger logger;

        protected readonly Dictionary<string, object> idObjs = new Dictionary<string, object>();
        protected ConditionalWeakTable<object, string> objsIds = new ConditionalWeakTable<object, string>();

        lock (_gate)
        {
            // Add type as a prefix to prevent collisions
            id = $"{obj.GetType().Name}_{id}";

            if (idObjs.ContainsKey(id))
            {
                logger.Warning("Duplicate id: {id}", id);
                return false;
            }

            if (objsIds.TryGetValue(obj, out var outvar))
            {
                logger.Warning("Object already registered: {ObjectType}", obj.GetType());
                return false;
            }

            IncrementCounter(obj);

            idObjs.Add(id, obj);
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

            newId = GenerateId(obj);

            if (idObjs.ContainsKey(newId))
            {
                logger.Error(
                    "Generated duplicate id {Id} for object type {ObjectType}",
                    newId,
                    obj.GetType());

                return false;
            }

            idObjs.Add(newId, obj);
            objsIds.Add(obj, newId);

            return true;
        }
    }

    private int IncrementCounter(object obj)
    {
        var type = obj.GetType();

        return objectCounters.AddOrUpdate(
            type,
            1,                // initial value if missing
            (_, current) => current + 1
        );
    }

    private string GenerateId(object obj)
    {
        var type = obj.GetType();
        return $"{type.Name}_{IncrementCounter(obj)}";
    }

    public bool Contains(object obj)
    {
        if (obj == null) return false;

        return objsIds.TryGetValue(obj, out var _);
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

        if (!idObjs.TryGetValue(id, out var storedObj)) return false;

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

        if (objsIds.TryGetValue(obj, out var id) == false) return false;

        lock (_gate)
        {
            return idObjs.Remove(id) && objsIds.Remove(obj);
        }
    }

    #region LogHelpers
    public bool TryGetIdWithLogging(object obj, out string id)
    {
        if (!TryGetId(obj, out id))
        {
            logger.Error(
                "[{ClassName}] Failed to get {ObjectType} in {ClassName}",
                nameof(ObjectManager),
                obj.GetType()
            );
        }

        private string GenerateId(object obj)
        {
            var type = obj.GetType();
            return $"{type.Name}_{IncrementCounter(obj)}";
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
