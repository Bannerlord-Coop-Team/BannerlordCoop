using Common.Messaging;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record AttemptJoin : ICommand
{
    public AttemptJoin(IPAddress address, int port, bool enableSteamInvites = false, string publicAddress = null)
        : this(address, port, null, enableSteamInvites, publicAddress)
    {
    }

    public AttemptJoin(IPAddress address, int port, string password,
        bool enableSteamInvites = false, string publicAddress = null)
    {
        Address = address;
        Port = port;
        Password = password;
        EnableSteamInvites = enableSteamInvites;
        PublicAddress = publicAddress;
    }

    public IPAddress Address { get; }
    public int Port { get; }
    public string Password { get; }
    public bool EnableSteamInvites { get; }
    public string PublicAddress { get; }
}
