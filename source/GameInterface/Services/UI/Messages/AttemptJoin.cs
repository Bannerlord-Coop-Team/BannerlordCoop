using Common.Messaging;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record AttemptJoin : ICommand
{
    public AttemptJoin(IPAddress address, int port, bool enableSteamInvites = false, string publicAddress = null)
    {
        Address = address;
        Port = port;
        EnableSteamInvites = enableSteamInvites;
        PublicAddress = publicAddress;
    }

    public IPAddress Address { get; }
    public int Port { get; }
    public bool EnableSteamInvites { get; }
    public string PublicAddress { get; }
}
