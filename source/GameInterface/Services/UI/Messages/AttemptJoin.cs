using Common.Messaging;
using Common.Network;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record AttemptJoin : ICommand
{
    public AttemptJoin(IPAddress address, int port)
    {
        Address = address;
        Port = port;
    }

    public IPAddress Address { get; }
    public int Port { get; }
}
