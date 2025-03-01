using Common.Messaging;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkEvent<TInstance, TValue> : IGenericBaseNetworkEvent
    {

        public abstract string InstanceId { get; set; }
        public abstract TValue Value { get; set; }
        
        public GenericNetworkEvent(string instanceId, TValue value)
        {
            InstanceId = instanceId;
            Value = value;
        }

        public abstract void HandleEvent(IObjectManager objectManager);
    }
}
