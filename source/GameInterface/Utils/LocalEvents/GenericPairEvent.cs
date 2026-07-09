using Common.Messaging;

namespace GameInterface.Utils.LocalEvents
{
    /// <summary>
    /// Local event carrying a key/value pair, published by the generated dictionary intercepts
    /// (Add and indexer set) in <see cref="GenericPatches{TPatch, TInstance}"/>.
    /// </summary>
    public record GenericPairEvent<TInstance, TKey, TValue> : IEvent
    {
        public TInstance Instance { get; }
        public TKey Key { get; }
        public TValue Value { get; }

        public GenericPairEvent(TInstance instance, TKey key, TValue value)
        {
            Instance = instance;
            Key = key;
            Value = value;
        }
    }
}
