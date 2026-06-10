namespace Missions.Services.Network.Enums
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
}
