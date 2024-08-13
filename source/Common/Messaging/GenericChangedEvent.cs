using ProtoBuf.Meta;

namespace Common.Messaging;

public class GenericChangedEvent<TClass> : ITargetEvent
{
    public object Value { get; }
    public string Id { get; }
    
    public string Target { get; set; }
    
    public GenericChangedEvent()
    {
    }

    public GenericChangedEvent(string id, object value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}

public class GenericChangedEvent<TClass, TValue> : ITargetEvent
{
    public TValue Value { get; }
    public string Id { get; }
    
    public string Target { get; set; }
    
    public GenericChangedEvent()
    {
    }

    public GenericChangedEvent(string id, TValue value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}