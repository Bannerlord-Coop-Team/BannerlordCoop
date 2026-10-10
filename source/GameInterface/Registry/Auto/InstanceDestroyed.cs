using Common.Messaging;

namespace GameInterface.Registry.Auto;
public readonly struct InstanceDestroyed<T> : IEvent
{
    public readonly T Instance;

    public InstanceDestroyed(T instance)
    {
        Instance = instance;
    }
}