using Common.Messaging;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkEvent<TInstance, TValue> : IEvent
    {
        public abstract string InstanceId { get; set; }

        public GenericNetworkEvent()
        {
        }

        public GenericNetworkEvent(string instanceId)
        {
            // Compact the id for the wire; the receiver re-adds the "{TInstance}_" prefix by type.
            InstanceId = ObjectManager.Compact(instanceId, typeof(TInstance));
        }
    }
}
