using Common.Messaging;

namespace GameInterface.Common.Events;

public class TextObjectEvent : ITargetEvent
{
    public string Id { get; }
    public string Target { get; }
    public string Value { get; }
}