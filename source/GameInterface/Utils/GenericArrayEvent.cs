using Common.Messaging;

namespace GameInterface.Utils
{
    public record GenericArrayEvent<TInstance, TValue> : IEvent
    {
        public GenericArrayEvent(TInstance instance, TValue value, int index)
        {
        }
    }
}
