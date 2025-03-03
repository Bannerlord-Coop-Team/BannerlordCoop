namespace GameInterface.Utils.NetworkEvents
{
    public abstract record GenericNetworkReferenceEvent<TInstance, TValue> : GenericNetworkEvent<TInstance, TValue>
    {
        public abstract string ValueId { get; set; }

        protected GenericNetworkReferenceEvent(string instanceId, string valueId) : base(instanceId)
        {
            ValueId = valueId;
        }
    }
}
