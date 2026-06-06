using Common.Messaging;

namespace GameInterface.Registry.Auto;
readonly struct InstanceDestroyed<T> : IEvent
{
    public readonly T Instance;

    public InstanceDestroyed(T instance)
    {
        Instance = instance;
    }
}