using System;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Server;

/// <summary>
/// Explains why the server could not bind its UDP port, for the bind failure log line.
/// </summary>
public interface IUdpBindDiagnostics
{
    /// <summary>
    /// Binds <paramref name="port"/> again the way LiteNetLib does and describes the result. Never throws.
    /// </summary>
    UdpBindFailure Describe(int port);
}

/// <summary>
/// A failed UDP bind as the server log reports it.
/// </summary>
public readonly struct UdpBindFailure
{
    /// <summary>The socket error name and its code, such as "AddressAlreadyInUse, 10048".</summary>
    public readonly string Error;
    /// <summary>The likely cause and what to do.</summary>
    public readonly string Detail;

    public UdpBindFailure(string error, string detail)
    {
        Error = error;
        Detail = detail;
    }
}

/// <inheritdoc cref="IUdpBindDiagnostics"/>
public class UdpBindDiagnostics : IUdpBindDiagnostics
{
    internal const string ReasonUnavailable = "reason unavailable";
    internal const string Closing = "Players cannot join this server. Close it, free the port and start it again.";

    public UdpBindFailure Describe(int port)
    {
        try
        {
            if (port <= 0) return new UdpBindFailure(ReasonUnavailable, Closing);

            SocketError? error = Probe(port);
            return new UdpBindFailure(FormatError(error), Format(port, error));
        }
        catch (Exception ex)
        {
            return new UdpBindFailure($"{ReasonUnavailable} ({ex.GetType().Name})", Closing);
        }
    }

    // Mirrors LiteNetLib's exclusive IPv4 bind, otherwise a holder on one address would read as a free port.
    internal static SocketError? Probe(int port)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.ExclusiveAddressUse = true;
        }
        catch (Exception)
        {
            // LiteNetLib ignores a failure here too; the bind still reports the error.
        }

        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return null;
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode;
        }
    }

    // The enum name and its fixed value, because SocketException.Message is localized and ErrorCode is errno on Unix.
    internal static string FormatError(SocketError? error) =>
        error.HasValue ? $"{error.Value}, {(int)error.Value}" : ReasonUnavailable;

    internal static string Format(int port, SocketError? error)
    {
        switch (error)
        {
            case null:
                return "the port can be opened now, so the failure was temporary (often a program that has since exited). " + Closing;
            case SocketError.AddressAlreadyInUse:
                return $"find the owner with \"netstat -ano -p udp\" (Windows) or \"ss -ulpn 'sport = :{port}'\" (Linux and Wine hosts). " + Closing;
            case SocketError.AccessDenied:
                return "the operating system refused the port: it may be in a Windows excluded port range " +
                    "(\"netsh interface ipv4 show excludedportrange protocol=udp\"), security software may have blocked this program, " +
                    "or on Linux ports below 1024 need extra privileges. " + Closing;
            default:
                return Closing;
        }
    }
}
