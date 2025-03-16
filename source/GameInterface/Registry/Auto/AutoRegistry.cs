using Common;
using Common.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Auto;


public interface IAutoRegistry<T> where T : class
{
    IEnumerable<MethodBase> Constructors { get; }
    IEnumerable<MethodBase> DestroyMethods { get; }
    void RegisterAllObjects(IRegistry<T> registry);
    void OnClientCreated(T obj, string id);
    void OnClientDestroyed(T obj, string id);

    void OnServerCreated(T obj, string id);
    void OnServerDestroyed(T obj, string id);
}

public class AutoRegistry<T> : RegistryBase<T> where T : class
{
    readonly static string InstanceId = $"Coop{typeof(T)}";

    static int InstanceCounter = 0;

    public Action<AutoRegistry<T>> RegisterAllCallback { get; }

    public AutoRegistry(Action<AutoRegistry<T>> registerAllCallback, IRegistryCollection collection) : base(collection)
    {
        RegisterAllCallback = registerAllCallback;
    }


    public override void RegisterAll()
    {
        RegisterAllCallback(this);
    }

    protected override string GetNewId(T obj)
    {
        var newId = $"{InstanceId}_{Interlocked.Increment(ref InstanceCounter)}";

        // Set object string id if it is a MBObjectBase
        // This is to keep the current network id in the save system so
        // save tranfers have enough data to resolve network ids
        if (obj is MBObjectBase mbObject)
        {
            using(new AllowedThread())
            {
                mbObject.StringId = newId;
            }
        }

        return newId;
    }
}