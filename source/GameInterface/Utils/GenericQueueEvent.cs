using Common.Messaging;

namespace GameInterface.Utils
{
    public record GenericQueueEvent<TInstance, TValue> : IEvent
    {
        /// <summary>
        /// Ctor used by GenericCollectionPatches to create the instance
        /// </summary>
        /// 
        public GenericQueueEvent()
        {
        }

        public GenericQueueEvent(TInstance instance, TValue value)
        {
            Instance = instance;
            Value = value;
        }

        public TInstance Instance { get; }
        public TValue Value { get; }
    }
}
