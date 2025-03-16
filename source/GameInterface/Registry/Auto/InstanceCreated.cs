using Common.Messaging;
using System;
using System.Runtime.CompilerServices;

namespace GameInterface.Registry.Auto;
class InstanceCreated<T> : IEvent where T : class
{
    public T Instance { get; }

    public InstanceCreated(T instance)
    {
        Instance = instance;
    }
}