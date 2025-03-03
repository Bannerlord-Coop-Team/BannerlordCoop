using Common.Messaging;

namespace GameInterface.Registry.Auto;
class InstanceCreated<T> : IEvent where T : class
{
    public T Instance { get; }

    public InstanceCreated(T instance)
    {
        Instance = instance;
    }
}