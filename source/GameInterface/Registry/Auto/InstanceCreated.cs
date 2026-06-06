using Common.Messaging;
using System;
using System.Runtime.CompilerServices;

namespace GameInterface.Registry.Auto;
readonly struct InstanceCreated<T> : IEvent
{
    public readonly T Instance;

    public InstanceCreated(T instance)
    {
        Instance = instance;
    }
}