using Common.Messaging;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record AttemptJoin : ICommand
{
    public AttemptJoin(IPAddress address, int port)
        : this(address, port, null)
    {
    }

    public AttemptJoin(IPAddress address, int port, string password)
    {
        Address = address;
        Port = port;
        Password = password;
    }

    public IPAddress Address { get; }
    public int Port { get; }
    public string Password { get; }
}
