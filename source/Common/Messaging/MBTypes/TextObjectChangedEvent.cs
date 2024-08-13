namespace Common.Messaging.MBTypes;

public class TextObjectChangedEvent<TClass> : GenericChangedEvent<TClass ,string>
{
    public TextObjectChangedEvent(string id, string value, string target) : base(id, value, target)
    {
    }
}