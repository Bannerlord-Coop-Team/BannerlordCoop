using System;
using System.Net;
using System.Net.Sockets;

namespace Coop.Steam;

/// <summary>
/// Loopback UDP socket helpers shared by the tunnel pumps: non-blocking sockets with
/// Windows' ICMP port-unreachable reset suppressed, and drain-style receives.
/// </summary>
internal static class TunnelSocket
{
    // SIO_UDP_CONNRESET: without this, a send to a not-yet-open loopback port makes the
    // next receive throw ConnectionReset instead of just dropping the datagram.
    private const int SioUdpConnReset = -1744830452;

    public static Socket CreateLoopbackDatagramSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false,
            ReceiveBufferSize = SteamTunnel.LoopbackBufferBytes,
            SendBufferSize = SteamTunnel.LoopbackBufferBytes,
        };

        // Best effort: the control code is Windows-only, and the receive helpers tolerate
        // the resets anyway.
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

    /// <summary>Returns the datagram length, 0 for a transient error worth skipping, -1 when drained.</summary>
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
