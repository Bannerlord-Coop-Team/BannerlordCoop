using Common.Messaging;

namespace GameInterface.Registry.Auto;
readonly struct InstanceCreated<T> : IEvent
{
    public readonly T Instance;

    public InstanceCreated(T instance)
    {
        Instance = instance;
    }
}