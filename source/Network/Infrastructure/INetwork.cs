namespace Network.Infrastructure
{
    public interface INetwork
    {
        bool IsConnected { get; }
        bool Connect();
        void Disconnect();
    }
}
