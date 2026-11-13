namespace Common.Messaging
{
    public sealed record MessagePayload<T> where T : IMessage
    {
        public object Who { get; }
        public T What { get; }

        public MessagePayload(object source, T payload)
        {
            Who = source; What = payload;
        }
    }
}
