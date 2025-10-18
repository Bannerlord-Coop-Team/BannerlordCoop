using Common.Messaging;

namespace GameInterface.Registry.Auto;
class NotifyInstanceCreated<T> : IEvent where T : class
{
    public string InstanceId { get; }
    public T Instance { get; }

    public NotifyInstanceCreated(string id, T instance)
    {
        InstanceId = id;
        Instance = instance;
    }
}