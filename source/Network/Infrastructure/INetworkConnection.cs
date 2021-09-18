using System;

namespace Network.Infrastructure
{
    public enum EDisconnectReason : byte
    {
        ConnectionRejected,
        ClientUnreachable,
        ClientLeft,
        ClientJoinedAnotherServer,
        ServerIsFull,
        Timeout,
        Unknown,
        Unreachable,
        WrongProtocolVersion,
        WrongGameVersion,
        IncompatibleMods,
        WorldDataTransferIssue,
        ServerShutDown
    }

    public enum EDeliveryMethod
    {
        Reliable,
        Unreliable
    }

    public interface INetworkConnection
    {
        int FragmentLength { get; }
        int MaxPackageLength { get; }
        int Latency { get; }
        void SendRaw(ArraySegment<byte> raw, EDeliveryMethod eMethod);
        void Close(EDisconnectReason eReason);
    }
}
