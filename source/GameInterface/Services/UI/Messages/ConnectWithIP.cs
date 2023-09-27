using Common.Messaging;
using Common.Network;
using System.Net;

namespace GameInterface.Services.UI.Messages;

public record ConnectWithIP : ICommand
{
    public ConnectWithIP(IPAddress address, int port)
    {
        Address = address;
        Port = port;
    }

    public IPAddress Address { get; }
    public int Port { get; }
}
