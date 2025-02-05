using Common;
using Common.Logging;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Registry;

internal abstract class RegistryBase<T> : IRegistry<T> where T : class
{
    protected readonly ILogger Logger = LogManager.GetLogger<RegistryBase<T>>();

    public IReadOnlyDictionary<string, T> Objects => objIds;

    protected readonly Dictionary<string, T> objIds = new Dictionary<string, T>();
    protected readonly ConditionalWeakTable<T, string> idObjs = new ConditionalWeakTable<T, string>();
    private readonly IRegistryCollection collection;

    protected RegistryBase(IRegistryCollection collection)
    {
        this.collection = collection;

        collection.AddRegistry(this);
    }

    public virtual void Dispose()
    {
        collection.RemoveRegistry(this);
    }

    /// <inheritdoc cref="IRegistry.RegisterAll"/>
    public abstract void RegisterAll();
    /// <summary>
    /// Generator function for unique object Ids
    /// </summary>
    /// <param name="obj">Object to create Id for</param>
    /// <returns>New unique id</returns>
    protected abstract string GetNewId(T obj);

    public int Count => objIds.Count;

    public virtual IEnumerable<Type> ManagedTypes { get; } = new Type[] { typeof(T) };

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => objIds.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => objIds.GetEnumerator();

    /// <inheritdoc cref="IRegistry"/>
    public virtual bool RegisterExistingObject(string id, object obj)
    {
        if (TryCast(obj, out var castedObj) == false) return false;

        if (objIds.ContainsKey(id))
        {
            Logger.Warning("{id} already exists in {type} Registry", id, typeof(T));
            return false;
        }

        if (obj is MBObjectBase mbObject)
        {
            mbObject.StringId = id;
        }

        objIds.Add(id, castedObj);
        idObjs.Add(castedObj, id);

        return true;
    }

    public virtual bool RegisterNewObject(object obj, out string id)
    {
        id = null;

        if (TryCast(obj, out T castedObj) == false) return false;

        var newId = GetNewId(castedObj);

        if (objIds.ContainsKey(newId)) return false;
        if (idObjs.TryGetValue(castedObj, out var _)) return false;

        if (obj is MBObjectBase mbObject)
        {
            mbObject.StringId = newId;
        }

        objIds.Add(newId, castedObj);
        idObjs.Add(castedObj, newId);

        id = newId;

        return true;
    }

    public virtual bool Remove(object obj) {
        if (TryCast(obj, out var castedObj) == false) return false;

        if (idObjs.TryGetValue(castedObj, out var id) == false) return false;

        return objIds.Remove(id) && idObjs.Remove(castedObj);
    }

    public virtual bool Remove(string id)
    {
        if (objIds.TryGetValue(id, out var obj) == false) return false;

        return objIds.Remove(id) && idObjs.Remove(obj);
    }

    public virtual bool TryGetId(object obj, out string id)
    {
        id = null;

        if (TryCast(obj, out var castedObj) == false) return false;

        return idObjs.TryGetValue(castedObj, out id);
    }

    public virtual bool TryGetValue<T1>(string id, out T1 obj) where T1 : class
    {
        obj = null;
        if (objIds.TryGetValue(id, out var internalobj) == false) return false;

        obj = internalobj as T1;
        return obj != null;
    }

    protected virtual bool TryCast(object obj, out T castedObj)
    {
        castedObj = obj as T;

        if (castedObj == null)
        {
            Logger.Error($"Attempted to get {obj.GetType()} from a registry that only accepts {typeof(T)}");
        }

        return castedObj != null;
    }
}
