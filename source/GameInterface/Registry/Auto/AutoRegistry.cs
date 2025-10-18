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
    private static readonly string InstanceId = $"Coop_{typeof(T).Name}";

    private static int InstanceCounter = 0;

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
        return $"{InstanceId}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}