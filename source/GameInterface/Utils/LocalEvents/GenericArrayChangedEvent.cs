using Common.Messaging;

namespace GameInterface.Utils.LocalEvents
{
    public abstract record GenericArrayChangedEvent<TInstance, TValue> : GenericEvent<TInstance, TValue>
    {
        public int Index { get; set; }

        public GenericArrayChangedEvent(TInstance instance, TValue value, int index) : base(instance, value)
        {
            Index = index;
        }
    }
}
