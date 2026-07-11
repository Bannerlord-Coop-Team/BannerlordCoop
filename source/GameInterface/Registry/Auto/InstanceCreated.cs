using Common.Messaging;

namespace GameInterface.Registry.Auto;
readonly struct InstanceCreated<T> : IEvent
{
    public readonly T Instance;
    public readonly object[] ConstructorArguments;

    public InstanceCreated(T instance, object[] constructorArguments = null)
    {
        Instance = instance;
        ConstructorArguments = constructorArguments;
    }
}
