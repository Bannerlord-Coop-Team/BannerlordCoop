using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Utils
{
    [ProtoContract(SkipConstructor = true)]
    public record GenericListEvent<TInstance, TValue> : IEvent
    {
        public GenericListEvent(TInstance instance, TValue value)
        {
        }
    }
}
