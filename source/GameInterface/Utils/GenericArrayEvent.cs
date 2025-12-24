using Common.Messaging;

namespace GameInterface.Utils
{
    public record GenericArrayEvent<TInstance, TValue> : IEvent
    {
        /// <summary>
        /// Default ctor used for testing
        /// </summary>
        public GenericArrayEvent()
        {
        }

        /// <summary>
        /// Ctor used by GenericCollectionPatches to create the instance
        /// </summary>
        public GenericArrayEvent(TInstance instance, TValue value, int index)
        {
            Instance = instance;
            Value = value;
            Index = index;
        }

        public TInstance Instance { get; }
        public TValue Value { get; }
        public int Index { get; }
    }
}
