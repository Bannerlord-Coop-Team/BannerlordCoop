using Common;
using Common.Logging;
using GameInterface.Services.Armies;
using GameInterface.Services.Clans;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Registry;
using GameInterface.Services.Settlements;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
    /// Attempts to get an object using a StringId and object type
    /// </summary>
    /// <typeparam name="T">Type of object</typeparam>
    /// <param name="id">StringId used to lookup object</param>
    /// <param name="obj">Out parameter for the object</param>
    /// <returns>True if successful, false if failed</returns>
    bool TryGetObject<T>(string id, out T obj) where T : MBObjectBase;

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
}

/// <summary>
/// Ground truth for storing and retreiving object and ids
/// </summary>
internal class ObjectManager : IObjectManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<ObjectManager>();

    private MBObjectManager objectManager => MBObjectManager.Instance;

    private readonly Dictionary<Type, IRegistry> RegistryMap = new Dictionary<Type, IRegistry>();

    public ObjectManager(
        IHeroRegistry heroRegistry,
        IMobilePartyRegistry partyRegistry, 
        IClanRegistry clanRegistry,
        IArmyRegistry armyRegistry)
    {
        RegistryMap.Add(heroRegistry.ManagedType, heroRegistry);
        RegistryMap.Add(partyRegistry.ManagedType, partyRegistry);
        RegistryMap.Add(clanRegistry.ManagedType, clanRegistry);
        RegistryMap.Add(armyRegistry.ManagedType, armyRegistry);
    }

    public bool AddExisting(string id, object obj)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (objectManager == null) return false;
        if (TryCastToMBObject(obj, out var mbObject) == false) return false;

        return AddExistingInternal(id, mbObject);
    }

    private bool TryCastToMBObject(object obj, out MBObjectBase mbObject)
    {
        mbObject = obj as MBObjectBase;

        if (mbObject == null)
        {
            Logger.Error("Attempted to register object with {type} type that does not derive from {mbObject}", obj.GetType(), typeof(MBObjectBase));
        }

        return mbObject != null;
    }

    private bool AddExistingInternal<T>(string id, T obj) where T : MBObjectBase
    {
        if (string.IsNullOrEmpty(id)) return false;

        obj.StringId = id;

        if (RegistryMap.TryGetValue(typeof(T), out IRegistry registry))
        {
            return registry.RegisterExistingObject(id, obj);
        }

        // Use MBObjectManager registry does not exist
        return objectManager.RegisterPresumedObject(obj) != null;
    }

    public bool AddNewObject(object obj, out string newId)
    {
        newId = null;

        if (objectManager == null) return false;
        if (TryCastToMBObject(obj, out var mbObject) == false) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return registry.RegisterNewObject(obj, out newId);
        }

        // Use MBObjectManager registry does not exist
        return AddNewObjectInternal(mbObject, out newId);
    }


    private static readonly MethodInfo RegisterObject = typeof(MBObjectManager)
        .GetMethod(nameof(MBObjectManager.RegisterObject));
    private bool AddNewObjectInternal(object obj, out string id)
    {
        id = null;

        if (objectManager == null) return false;
        if (TryCastToMBObject(obj, out var mbObject) == false) return false;

        RegisterObject.MakeGenericMethod(obj.GetType()).Invoke(objectManager, new object[] { mbObject });

        id = mbObject.StringId;

        return true;
    }

    public bool Contains(object obj)
    {
        if (objectManager == null) return false;
        if (TryCastToMBObject(obj, out var mbObject) == false) return false;

        if (RegistryMap.TryGetValue(obj.GetType(), out IRegistry registry))
        {
            return registry.TryGetId(obj, out _);
        }

        // Attempt to find using string id instead
        return Contains(mbObject.StringId);
    }

    
    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (objectManager == null) return false;

        if (RegistryMap.Values.Any(registry => registry.TryGetId(id, out _))) return true;

        // Use MBObjectManager registry cannot find value
        return objectManager.Contains(id);
    }

    public bool TryGetId(object obj, out string id)
    {
        id = null;

        if (objectManager == null) return false;
        if (TryCastToMBObject(obj, out var mbObject) == false) return false;

        id = mbObject.StringId;

        return true;
    }

    private static readonly MethodInfo GetObject = typeof(MBObjectManager)
        .GetMethod(nameof(MBObjectManager.GetObject), new Type[] { typeof(string) });
    public bool TryGetObject<T>(string id, out T obj) where T : MBObjectBase
    {
        obj = default;

        if (string.IsNullOrEmpty(id)) return false;
        if (objectManager == null) return false;

        if (RegistryMap.TryGetValue(typeof(T), out IRegistry registry))
        {
            if (registry.TryGetValue<T>(id, out var registeredObj) == false) return false;

            obj = registeredObj as T;
            return obj != null;
        }

        obj = (T)GetObject.MakeGenericMethod(typeof(T)).Invoke(objectManager, new object[] { id });

        return obj != null;
    }
}