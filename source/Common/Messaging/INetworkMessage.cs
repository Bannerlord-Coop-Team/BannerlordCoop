using LiteNetLib;

namespace Common.Messaging
{
    public interface INetworkMessage
    {
        object Data { get; set; }
    }
}
