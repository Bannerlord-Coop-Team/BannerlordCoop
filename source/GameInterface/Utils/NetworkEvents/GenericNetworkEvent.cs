using Common.Messaging;

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
            InstanceId = instanceId;
        }
    }
}
