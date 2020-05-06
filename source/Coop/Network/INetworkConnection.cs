using System;

namespace Coop.Network
{
    public enum EDisconnectReason : byte
    {
        ClientLeft,
        ClientJoinedAnotherServer,
        ServerIsFull,
        ServerShutDown,
        WrongProtocolVersion,
        WorldDataTransferIssue,
        Unknown
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
