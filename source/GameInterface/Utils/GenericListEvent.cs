using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Utils
{
    [ProtoContract(SkipConstructor = true)]
    public record GenericListEvent<TInstance, TValue> : IEvent
    {
        /// <summary>
        /// Ctor used by GenericCollectionPatches to create the instance
        /// </summary>
        /// 
        public GenericListEvent()
        {
        }

        public GenericListEvent(TInstance instance, TValue value)
        {
            Instance = instance;
            Value = value;
        }

        public TInstance Instance { get; }
        public TValue Value { get; }
    }
}
