using Common.Messaging;

namespace GameInterface.Registry.Auto;
class InstanceDestroyed<T> : IEvent where T : class
{
    public T Instance { get; }

    public InstanceDestroyed(T instance)
    {
        Instance = instance;
    }
}