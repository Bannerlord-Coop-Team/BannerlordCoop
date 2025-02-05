using Common;
using Common.Logging;
using GameInterface.Services.Registry;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TaleWorlds.ObjectSystem;

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
    bool IsTypeManaged(Type type);
}

/// <summary>
/// Ground truth for storing and retreiving object and ids
/// </summary>
internal class ObjectManager : IObjectManager
{
    private readonly ILogger logger;

    private readonly GameObjectManager defaultObjectManager;

    IReadOnlyDictionary<Type, IRegistry> RegistryMap => registryCollection.RegistryMap;

    public ObjectManager(IRegistryCollection registryCollection, ILogger logger)
    {
        this.registryCollection = registryCollection;
        this.logger = logger;

        defaultObjectManager = new GameObjectManager(logger);
    }

    public bool AddExisting(string id, object obj)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (obj == null) return false;
        
        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return LogIfRegistrationError(
                registry.RegisterExistingObject(id, obj),
                obj);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return LogIfRegistrationError(
            defaultObjectManager.AddExisting(id, obj),
            obj);
    }

    public bool AddNewObject(object obj, out string newId )
    {
        newId = null;
        if (obj == null) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return LogIfRegistrationError(
                registry.RegisterNewObject(obj, out newId),
                obj);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return LogIfRegistrationError(
            defaultObjectManager.AddNewObject(obj, out newId),
            obj);
    }

    public bool Contains(object obj)
    {
        if (obj == null) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return registry.TryGetId(obj, out _);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return defaultObjectManager.Contains(obj);
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (RegistryMap.Values.Any(registry => registry.TryGetId(id, out _))) return true;

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return defaultObjectManager.Contains(id);
    }

    public bool TryGetId(object obj, out string id)
    {
        id = null;
        if (obj == null) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return registry.TryGetId(obj, out id);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return defaultObjectManager.TryGetId(obj, out id);
    }

    private static readonly MethodInfo GetObject = typeof(MBObjectManager)
        .GetMethod(nameof(MBObjectManager.GetObject), new Type[] { typeof(string) });
    private readonly IRegistryCollection registryCollection;

    public bool TryGetObject<T>(string id, out T obj) where T : class
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;

        if (RegistryMap.TryGetValue(typeof(T), out IRegistry registry))
        {
            return registry.TryGetValue(id, out obj);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return defaultObjectManager.TryGetObject(id, out obj);
    }

    public bool Remove(object obj)
    {
        if (obj == null) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return registry.Remove(obj);
        }

        /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
        return defaultObjectManager.Remove(obj);
    }

    public bool IsTypeManaged(Type type)
    {
        return RegistryMap.ContainsKey(type) || defaultObjectManager.IsTypeManaged(type);
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


    /// <summary>
    /// Bannerlord internal object manager
    /// </summary>
    private class GameObjectManager : IObjectManager
    {
        private MBObjectManager objectManager => MBObjectManager.Instance;

        private static readonly MethodInfo RegisterObject = typeof(MBObjectManager)
            .GetMethod(nameof(MBObjectManager.RegisterObject));

        public bool AddExisting(string id, object obj)
        {
            if (objectManager == null) return false;

            if (TryCastToMBObject(obj, out var mbObject) == false) return false;
            mbObject.StringId = id;

            return RegisterExistingObjectMethod.MakeGenericMethod(obj.GetType()).Invoke(objectManager, new object[] { obj }) != null;
        }

        private readonly MethodInfo RegisterExistingObjectMethod = AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.RegisterPresumedObject));
        private readonly ILogger logger;

        public GameObjectManager(ILogger logger)
        {
            this.logger = logger;
        }

        private T Cast<T>(object obj)
        {
            return (T)obj;
        }

        public bool AddNewObject(object obj, out string newId)
        {
            newId = null;
            if (objectManager == null) return false;

            /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
            if (TryCastToMBObject(obj, out var mbObject) == false) return false;

            RegisterObject.MakeGenericMethod(obj.GetType()).Invoke(objectManager, new object[] { mbObject });

            newId = mbObject.StringId;

            return true;
        }

        public bool Contains(object obj)
        {
            if (objectManager == null) return false;

            if (TryCastToMBObject(obj, out var mbObject) == false) return false;

            // Attempt to find using string id instead
            return Contains(mbObject.StringId);
        }

        public bool Contains(string id) => objectManager?.ObjectTypeRecords.Any(x => x.ContainsObject(id)) ?? false;

        public bool Remove(object obj)
        {
            if (objectManager == null) return false;

            /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
            if (TryCastToMBObject(obj, out var mbObject) == false) return false;
            objectManager.UnregisterObject(mbObject);

            return true;
        }


        public bool TryGetId(object obj, out string id)
        {
            id = null;
            if (objectManager == null) return false;

            /// Default object manager <see cref="MBObjectManager"/> requires type to be <see cref="MBObjectBase"/>
            if (TryCastToMBObject(obj, out var mbObject) == false) return false;

            id = mbObject.StringId;

            return true;
        }

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            obj = null;
            if (objectManager == null) return false;

            if (typeof(MBObjectBase).IsAssignableFrom(typeof(T)) == false) return false;

            obj = (T)GetObject.MakeGenericMethod(typeof(T)).Invoke(objectManager, new object[] { id });

            return obj != null;
        }

        private bool TryCastToMBObject(object obj, out MBObjectBase mbObject)
        {
            mbObject = obj as MBObjectBase;

            if (mbObject == null)
            {
                logger.Error("Attempted to register object with {type} type that does not derive from {mbObject}", obj.GetType(), typeof(MBObjectBase));
            }

            return mbObject != null;
        }

        public bool IsTypeManaged(Type type) => objectManager?.HasType(type) ?? false;
    }
}