using Common.Messaging;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericReferenceNetworkEvent<TInstance, TValue> : IGenericBaseNetworkEvent
    {

        public abstract string InstanceId { get; set; }
        public abstract string ValueId { get; set; }
        
        public GenericReferenceNetworkEvent(string instanceId, string valueId)
        {
            InstanceId = instanceId;
            ValueId = valueId;
        }

        public abstract void HandleEvent(IObjectManager objectManager);
    }
}
