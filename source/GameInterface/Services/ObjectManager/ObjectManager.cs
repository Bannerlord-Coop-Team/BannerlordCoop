using Common;
using GameInterface.Registry;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

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
    /// Attempts to get an object using a StringId and object type
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>
    /// <param name="id">StringId used to lookup object</param>
    /// <param name="obj">Out parameter for the object (null if not found)</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetObject<T>(string id, out T obj) where T : class;

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
internal class ObjectManager : IObjectManager
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
        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        if (string.IsNullOrEmpty(id))
        {
            if (registry.TryGetId(obj, out var existingId))
            {
                id = existingId;
            }
            else
            {
                var mBObj = obj as MBObjectBase;
                if (mBObj != null)
                {
                    var stringId = mBObj.StringId;
                    if (!string.IsNullOrEmpty(stringId))
                    {
                        id = stringId;
                    }
                    else
                    {
                        var guid = mBObj.Id;
                        if (guid != default)
                        {
                            id = typeof(T).Name + "_" + guid.InternalValue.ToString();
                        }
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    var objectType = obj.GetType();
                    var className = nameof(ObjectManager);
                    var stackTrace = Environment.StackTrace;
                    logger.Error("Unable to derive id for {name} in {objectManager}\nStackTrace: {stackTrace}", objectType, className, stackTrace);
                    return false;
                }
            }
        }

        if (Contains(id)) return true;

        return LogIfRegistrationError(
            registry.RegisterExistingObject(id, obj),
            obj);
    }

    public bool AddNewObject<T>(T obj, out string newId )
    {
        newId = null;
        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return LogIfRegistrationError(
            registry.RegisterNewObject(obj, out newId),
            obj);
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

        if (RegistryMap.Values.Any(registry => registry.TryGetValue<object>(id, out _))) return true;

        return false;
    }

    public bool TryGetId<T>(T obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;

        return registry.TryGetId(obj, out id);
    }

    private readonly IRegistryCollection registryCollection;

    public bool TryGetObject<T>(string id, out T obj) where T : class
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        if (TryGetRegistry(typeof(T), out IRegistry registry) == false) return false;
        if (registry.TryGetValue(id, out obj)) return true;

        object candidate = null;

        var com = Campaign.Current?.CampaignObjectManager;
        if (com != null)
        {
            try
            {
                var find = com.GetType().GetMethod("Find")?.MakeGenericMethod(typeof(T));
                candidate = find?.Invoke(com, new object[] { id });
            }
            catch { }
        }

        if (candidate == null)
        {
            var mb = MBObjectManager.Instance;
            if (mb != null)
            {
                try
                {
                    var get = mb.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetObject" && m.IsGenericMethod)?.MakeGenericMethod(typeof(T));
                    candidate = get?.Invoke(mb, new object[] { id });
                }
                catch { }
            }
        }

        if (candidate is T found)
        {
            registry.RegisterExistingObject(id, found);
            obj = found;
            return true;
        }

        return false;
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
    private bool LogIfRegistrationError(bool result, object registerObject)
    {
        if (result) return true;

        var objectType = registerObject.GetType();
        var className = nameof(ObjectManager);
        var stackTrace = Environment.StackTrace;

        logger.Error("Unable to register {name} with {objectManager}\n" +
                     "StackTrace: {stackTrace}",
                     objectType,
                     className,
                     stackTrace);

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
