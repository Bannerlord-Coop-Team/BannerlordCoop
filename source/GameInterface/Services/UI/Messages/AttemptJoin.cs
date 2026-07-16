using Common.Messaging;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record AttemptJoin : ICommand
{
    public AttemptJoin(IPAddress address, int port, bool enableSteamInvites = false)
        : this(address, port, null, enableSteamInvites)
    {
    }

    public AttemptJoin(IPAddress address, int port, string password,
        bool enableSteamInvites = false)
    {
        Address = address;
        Port = port;
        Password = password;
        EnableSteamInvites = enableSteamInvites;
    }

    public IPAddress Address { get; }
    public int Port { get; }
    public string Password { get; }
    public bool EnableSteamInvites { get; }
}
