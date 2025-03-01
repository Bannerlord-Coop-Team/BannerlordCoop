using GameInterface.Services.Registry;
using System;
using System.Threading;

namespace GameInterface.AutoSync.Registry;

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
        return $"{InstanceId}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}