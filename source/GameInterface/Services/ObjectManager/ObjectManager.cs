using Common;
using GameInterface.Registry;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using TaleWorlds.Core;

namespace GameInterface.Services.ObjectManager;

public interface IObjectManager
{
    /// <summary>
    /// Determins if an object is stored in the object manager
    /// </summary>
    /// <param name="obj">Object to check if stored</param>
    /// <returns>True if stored, false if not</returns>
    bool Contains<T>(T obj);

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
    bool TryGetId<T>(T obj, out string id);

    /// <summary>
    /// Attempts to get an object's StringId from the object itself
    /// </summary>
    /// <param name="obj">Object to get StringId from</param>
    /// <param name="id">Out parameter for string id (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetId(Type type, object obj, out string id);

    /// <summary>
    /// Attempts to get an object using a StringId and object type
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>
    /// <param name="id">StringId used to lookup object</param>
    /// <param name="obj">Out parameter for the object (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetObject<T>(string id, out T obj) where T : class;

    /// <summary>
    /// Attempts to get an object using a StringId and object type
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>
    /// <param name="id">StringId used to lookup object</param>
    /// <param name="obj">Out parameter for the object (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetObject(Type type, string id, out object obj);

    /// <summary>
    /// Add an object with already existing StringId
    /// </summary>
    /// <param name="id">Id to assosiate with object</param>
    /// <param name="obj">Object to assosiate with id</param>
    /// <returns>True if successful, false if failed</returns>
    bool AddExisting<T>(string id, T obj);

    /// <summary>
    /// Adds an object without a registered StringId
    /// </summary>
    /// <param name="obj">Object to register</param>
    /// <param name="newId">Newly created StringId</param>
    /// <returns>True if successful, false if failed</returns>
    bool AddNewObject<T>(T obj, out string newId);

    /// <summary>
    /// Removes an object from the <see cref="IObjectManager"/>
    /// </summary>
    /// <param name="obj">Object to remove</param>
    /// <returns>True if successful, false if failed</returns>
    bool Remove<T>(T obj);
    bool IsTypeManaged(Type type);
}

/// <summary>
/// Ground truth for storing and retreiving object and ids
/// </summary>
public class ObjectManager : IObjectManager
{
    private readonly ILogger logger;

    IReadOnlyDictionary<Type, IRegistry> RegistryMap => registryCollection.RegistryMap;

    public ObjectManager(IRegistryCollection registryCollection, ILogger logger)
    {
        this.registryCollection = registryCollection;
        this.logger = logger;
    }

    public bool AddExisting<T>(string id, T obj)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return LogIfRegistrationError(
            registry.RegisterExistingObject(id, obj),
            obj,
            id);
    }

    public bool AddNewObject<T>(T obj, out string newId)
    {
        newId = null;
        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return LogIfRegistrationError(
            registry.RegisterNewObject(obj, out newId),
            obj,
            newId);
    }

    public bool Contains<T>(T obj)
    {
        if (obj == null) return false;

        if (TryGetRegistry(obj.GetType(), out IRegistry registry) == false) return false;

        return registry.TryGetId(obj, out _);
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (RegistryMap.Values.Any(registry => registry.TryGetId(id, out _))) return true;

        return false;
    }

    public bool TryGetId<T>(T obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return registry.TryGetId(obj, out id);
    }

    public bool TryGetId(Type type, object obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        if (TryGetRegistry(type, out IRegistry registry) == false) return false;

        return registry.TryGetId(obj, out id);
    }

    private readonly IRegistryCollection registryCollection;

    public bool TryGetObject<T>(string id, out T obj) where T : class
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return registry.TryGetValue(id, out obj);
    }
    public bool TryGetObject(Type type, string id, out object obj)
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        if (TryGetRegistry(type, out IRegistry registry) == false) return false;

        return registry.TryGetValue(id, out obj);
    }

    public bool Remove<T>(T obj)
    {
        if (obj == null) return false;

        if (TryGetRegistry(obj.GetType(), out IRegistry registry) == false) return false;

        return registry.Remove(obj);
    }

    public bool IsTypeManaged(Type type) => RegistryMap.ContainsKey(type);

    private bool TryGetRegistry(Type type, out IRegistry registry)
    {
        if (RegistryMap.TryGetValue(type, out registry) == false)
        {
            logger.Error($"{nameof(ObjectManager)} was unable to find Registry of type {type}");
            return false;
        }

        return true;
    }

    #region LogHelpers
    private bool LogIfRegistrationError(bool result, object registerObject, object registerId)
    {
        if (result) return true;

        var objectType = registerObject.GetType();

        string objectId = "Unknown";
        if (registerId != null)
        {
            objectId = registerId.ToString();
        }

        var className = nameof(ObjectManager);

        logger.Error("Unable to register {name} with id: {id} in {objectManager}",
                     objectType,
                     objectId,
                     className);

        return false;
    }

    private bool LogIfGetError(bool result, object objToGet)
    {
        if (result) return true;

        var objectType = objToGet.GetType();
        var className = nameof(ObjectManager);
        var stackTrace = Environment.StackTrace;

        logger.Error("Unable to get {name} with {objectManager}\n" +
                     "StackTrace: {stackTrace}",
                     objectType,
                     className,
                     stackTrace);

        return false;
    }

    private bool LogIfGetError<T>(bool result, string id) where T : class
    {
        if (result) return true;

        var objectType = typeof(T);
        var stringId = id;
        var className = nameof(ObjectManager);
        var stackTrace = Environment.StackTrace;

        logger.Error("Unable to get {name} with {stringId} in {objectManager}\n" +
                     "StackTrace: {stackTrace}",
                     objectType,
                     stringId,
                     className,
                     stackTrace);

        return false;
    }
    #endregion

}