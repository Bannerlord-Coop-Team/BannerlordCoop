namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkReferenceEvent<TInstance, TValue> : GenericNetworkEvent<TInstance, TValue>
    {
        public abstract uint ValueId { get; set; }

        protected GenericNetworkReferenceEvent(uint instanceId, uint valueId) : base(instanceId)
        {
            ValueId = valueId;
        }
    }
}
