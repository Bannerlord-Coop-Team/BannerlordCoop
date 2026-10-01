using Common.Messaging;
namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkEvent<TInstance, TValue> : IEvent
    {
        public abstract uint InstanceId { get; set; }

        public GenericNetworkEvent()
        {
        }

        public GenericNetworkEvent(uint instanceId)
        {
            InstanceId = instanceId;
        }
    }
}
