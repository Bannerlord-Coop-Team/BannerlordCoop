using Common;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace GameInterface.Registry.Auto;


public interface IAutoRegistry<T> where T : class
{
    IEnumerable<MethodBase> Constructors { get; }
    IEnumerable<MethodBase> DestroyMethods { get; }
    void RegisterAllObjects(IObjectManager objectManager);
    void OnClientCreated(T obj, string id);
    void OnClientDestroyed(T obj, string id);

    void OnServerCreated(T obj, string id);
    void OnServerDestroyed(T obj, string id);
}

public class AutoRegistry<T> : RegistryBase<T> where T : class
{
    private static readonly string InstanceId = $"Coop_{typeof(T)}";
    private readonly IObjectManager objectManager;
    private static int InstanceCounter = 0;

    public Action<IObjectManager> RegisterAllCallback { get; }

    public AutoRegistry(Action<IObjectManager> registerAllCallback, IRegistryCollection collection, IObjectManager objectManager) : base(collection)
    {
        RegisterAllCallback = registerAllCallback;
        this.objectManager = objectManager;
    }


    public override void RegisterAll()
    {
        RegisterAllCallback(objectManager);
    }

    protected override string GetNewId(T obj)
    {
        return $"{InstanceId}_{Interlocked.Increment(ref InstanceCounter)}";
    }

    public override bool RegisterExistingObject(string id, T obj)
    {
        // Parse the numeric suffix from the Coop ID ("Coop_{Type}_{N}") and advance the
        // counter to at least N. Without this, a 3rd autoconnect client's GetNewId would
        // collide with IDs already registered by earlier clients via NetworkCreateInstance.
        if (id != null)
        {
            var lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(id.Substring(lastUnderscore + 1), out var parsedNumber))
            {
                int current;
                do
                {
                    current = InstanceCounter;
                    if (current >= parsedNumber) break;
                }
                while (Interlocked.CompareExchange(ref InstanceCounter, parsedNumber, current) != current);
            }
            else
            {
                Interlocked.Increment(ref InstanceCounter);
            }
        }
        return base.RegisterExistingObject(id, obj);
    }

    public override bool RegisterNewObject(T obj, out string id)
    {
        return base.RegisterNewObject(obj, out id);
    }
}