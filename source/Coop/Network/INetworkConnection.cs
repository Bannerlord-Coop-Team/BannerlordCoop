using System;

namespace Coop.Network
{
    public enum EDisconnectReason : byte
    {
        ConnectionRejected,
        ClientLeft,
        ClientJoinedAnotherServer,
        ServerIsFull,
        ServerShutDown,
        Timeout,
        Unknown,
        Unreachable,
        WrongProtocolVersion,
        WorldDataTransferIssue,
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
