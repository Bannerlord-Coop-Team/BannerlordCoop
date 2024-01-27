using Common;
using Common.Logging;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Registry;

internal abstract class RegistryBase<T> : IRegistry<T> where T : MBObjectBase
{
    protected readonly ILogger Logger = LogManager.GetLogger<RegistryBase<T>>();

    protected readonly Dictionary<string, T> objIds = new Dictionary<string, T>();

    public int Count => objIds.Count;

    public Type ManagedType => typeof(T);

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => objIds.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => objIds.GetEnumerator();

    /// <inheritdoc cref="IRegistry"/>
    public virtual bool RegisterExistingObject(string id, object obj)
    {
        if (TryCast(obj, out var castedObj) == false) return false;

        if (objIds.ContainsKey(id))
        {
            Logger.Warning("{id} already exists in {type} Registry", castedObj.StringId, typeof(T));
            return false;
        }

        objIds.Add(id, castedObj);

        return true;
    }


    public abstract bool RegisterNewObject(object obj, out string id);
    /// <summary>
    /// Handles common functionality of registering new <see cref="T"/>
    /// </summary>
    /// <param name="obj">Object to register</param>
    /// <param name="stringIdPrefix">Prefix of <see cref="T.StringId"/></param>
    /// <param name="id">Out parameter of newly created <see cref="T.StringId"/></param>
    /// <returns>True if creation was successful, otherwise false</returns>
    protected virtual bool RegisterNewObject(object obj, string stringIdPrefix, out string id)
    {
        id = null;

        if (Campaign.Current?.CampaignObjectManager == null) return false;
        if (TryCast(obj, out T castedObj) == false) return false;

        var newId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<T>(stringIdPrefix);

        if (objIds.ContainsKey(newId)) return false;

        castedObj.StringId = newId;

        objIds.Add(newId, castedObj);

        id = newId;

        return true;
    }

    public virtual bool Remove(object obj) {
        if (TryCast(obj, out var castedObj) == false) return false;
        return objIds.Remove(castedObj.StringId);
    }

    public virtual bool Remove(string id) => objIds.Remove(id);

    public virtual bool TryGetValue(object obj, out string id)
    {
        id = null;

        if (TryCast(obj, out var castedObj) == false) return false;
        if (objIds.ContainsKey(castedObj.StringId) == false) return false;

        id = castedObj.StringId;
        return true;
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
