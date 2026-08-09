using System;
using System.Net;
using System.Net.Sockets;

namespace Common.Network.Session;

internal static class LoopbackDatagramSocket
{
    private const int SioUdpConnReset = -1744830452;

    public static Socket Create()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false,
            ReceiveBufferSize = ProviderTunnel.LoopbackBufferBytes,
            SendBufferSize = ProviderTunnel.LoopbackBufferBytes,
        };

        try
        {
            socket.IOControl(SioUdpConnReset, new byte[] { 0 }, null);
        }
        catch (SocketException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }

    public static EndPoint AnyEndpoint() => new IPEndPoint(IPAddress.Any, 0);

    public static int TryReceiveFrom(Socket socket, byte[] buffer, ref EndPoint sender)
    {
        try
        {
            if (socket.Available == 0) return -1;

            return socket.ReceiveFrom(buffer, ref sender);
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode == SocketError.WouldBlock ? -1 : 0;
        }
    }
}
