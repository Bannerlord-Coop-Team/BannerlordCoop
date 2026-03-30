using Common.Messaging;

namespace GameInterface.Utils.LocalEvents
{
    public record GenericEvent<TInstance, TValue> : IEvent
    {
        public TInstance Instance { get; }
        public TValue Value { get; }

        public GenericEvent()
        {
        }

        public GenericEvent(TInstance instance, TValue value)
        {
            Instance = instance;
            Value = value;
        }
    }
}
