namespace Coop.Network
{
    public interface INetwork
    {
        bool IsConnected { get; }
        bool Connect();
        void Disconnect();
    }
}
