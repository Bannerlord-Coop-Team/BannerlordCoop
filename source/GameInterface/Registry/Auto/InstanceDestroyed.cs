using Common.Messaging;

namespace GameInterface.Registry.Auto;
class InstanceDestroyed<T> : IEvent
{
    public T Instance { get; }

    public InstanceDestroyed(T instance)
    {
        Instance = instance;
    }
}