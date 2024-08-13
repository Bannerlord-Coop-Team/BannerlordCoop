using Common.Messaging;

namespace GameInterface.Common.Commands;

/// <summary>
/// Generic client publish
/// </summary>
public class GenericChangeCommand<TClass> : ITargetCommand
{
    public object Value { get; }
    public string Id { get; }
    
    public string Target { get; }

    public GenericChangeCommand(string id, object value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}

/// <summary>
/// Generic client publish with generic value type.
/// </summary>
public class GenericChangeCommand<TClass, TValue> : ITargetCommand
{
    public TValue Value { get; }
    public string Id { get; }
    
    public string Target { get; }

    public GenericChangeCommand(string id, TValue value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}