using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkReferenceEvent<TInstance, TValue> : GenericNetworkEvent<TInstance, TValue>
    {
        public abstract string ValueId { get; set; }

        protected GenericNetworkReferenceEvent(string instanceId, string valueId) : base(instanceId)
        {
            // Compact the id for the wire; the receiver re-adds the "{TValue}_" prefix by type.
            ValueId = ObjectManager.Compact(valueId, typeof(TValue));
        }
    }
}
