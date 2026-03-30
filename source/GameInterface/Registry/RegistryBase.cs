using Common;
using Common.Logging;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;

namespace GameInterface.Registry;

public abstract class RegistryBase<T> : IRegistry<T> where T : class
{
    protected readonly ILogger Logger = LogManager.GetLogger<RegistryBase<T>>();

    public IReadOnlyDictionary<string, T> Objects => idObjs.GetReadOnlyDictionary();

    /// <inheritdoc cref="IRegistry.Count"/>
    public int Count => Objects.Count;

    //protected readonly Dictionary<string, WeakReference<T>> objIds = new Dictionary<string, WeakReference<T>>();
    protected readonly Dictionary<string, T> idObjs = new Dictionary<string, T>();
    protected ConditionalWeakTable<T, string> objsIds = new ConditionalWeakTable<T, string>();
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

    public virtual IEnumerable<Type> ManagedTypes { get; } = new Type[] { typeof(T) };

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => Objects.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Objects.GetEnumerator();

    /// <inheritdoc cref="IRegistry"/>
    public virtual bool RegisterExistingObject(string id, object obj)
    {
        if (TryCast(obj, out var castedObj) == false) {
            LogObjectRegistration(obj);
            Logger.Warning("Failed to cast {type} to {castType}", obj.GetType(), typeof(T));
            return false;
        }

        if (idObjs.ContainsKey(id))
        {
            LogObjectRegistration(obj);
            Logger.Warning("{id} already exists in {type} Registry", id, typeof(T));
            return false;
        }

        idObjs.Add(id, castedObj);
        objsIds.Add(castedObj, id);

        return true;
    }

    public virtual bool RegisterNewObject(object obj, out string id)
    {
        id = null;

        if (TryCast(obj, out T castedObj) == false)
        {
            LogObjectRegistration(obj);
            Logger.Warning("Failed to cast {type} to {castType}", obj.GetType(), typeof(T));
            return false;
        }

        var newId = GetNewId(castedObj);

        if (idObjs.ContainsKey(newId))
        {
            LogObjectRegistration(obj);
            Logger.Warning("id ({newId}): was already registered for type ({type})", newId, obj.GetType());
            return false;
        }

        if (objsIds.TryGetValue(castedObj, out var outvar))
        {
            LogObjectRegistration(obj);
            Logger.Warning("object was already registered for type ({type})", newId, obj.GetType());
            return false;
        }

        idObjs.Add(newId, castedObj);
        objsIds.Add(castedObj, newId);

        id = newId;

        return true;
    }

    public virtual bool Remove(object obj)
    {
        if (TryCast(obj, out var castedObj) == false) return false;

        if (objsIds.TryGetValue(castedObj, out var id) == false) return false;

        return idObjs.Remove(id) && objsIds.Remove(castedObj);
    }

    public virtual bool Remove(string id)
    {
        if (idObjs.TryGetValue(id, out var obj) == false) return false;

        return idObjs.Remove(id) && objsIds.Remove(obj);
    }

    public virtual bool TryGetId(object obj, out string id)
    {
        id = null;

        if (TryCast(obj, out var castedObj) == false) return false;

        return objsIds.TryGetValue(castedObj, out id);
    }

    public virtual bool TryGetValue<T1>(string id, out T1 obj) where T1 : class
    {
        obj = null;
        if (idObjs.TryGetValue(id, out var internalobj) == false) return false;

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

    /// <inheritdoc />
    public void Clear()
    {
        idObjs.Clear();
        objsIds = new ConditionalWeakTable<T, string>();
    }

    // Log which function called either RegisterNewObject or RegisterExistingObject method and the class it belongs to
    private void LogObjectRegistration(object obj, [CallerMemberName] string registrar = "")
    {
        MethodBase method = new StackFrame(3, false).GetMethod();

        string callingMethod = "Unknown";
        string callingClass = "Unknown";
        if (callingMethod != null)
        {
            callingMethod = method.Name;
            callingClass = method.DeclaringType.Name;
        }
        Logger.Debug("{Registrar} called by: {CallingMethod} of class: {CallingClass} for object: {ObjectType}", registrar, callingMethod, callingClass, obj.GetType());
    }
}
