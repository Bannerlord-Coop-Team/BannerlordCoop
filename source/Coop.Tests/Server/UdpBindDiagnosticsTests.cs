using Coop.Core.Server;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Coop.Tests.Server;

/// <summary>Verifies the bind failure probe and the log text it produces.</summary>
public class UdpBindDiagnosticsTests
{
    private const string Closing = UdpBindDiagnostics.Closing;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Describe_InvalidPort_SkipsTheProbe(int port)
    {
        UdpBindFailure failure = new UdpBindDiagnostics().Describe(port);

        Assert.Equal("reason unavailable", failure.Error);
        Assert.Equal(Closing, failure.Detail);
    }

    [Fact]
    public void Describe_HolderOnOneAddress_ReportsTheBindError()
    {
        // Default options on a specific address; a default-option wildcard probe could share the port on Windows.
        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)holder.LocalEndPoint!).Port;

        UdpBindFailure failure = new UdpBindDiagnostics().Describe(port);

        Assert.Equal("AddressAlreadyInUse, 10048", failure.Error);
        Assert.DoesNotContain("can be opened now", failure.Detail);
    }

    [Fact]
    public void Describe_FreedPort_ReadsAsTemporary()
    {
        int port;
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        UdpBindFailure failure = new UdpBindDiagnostics().Describe(port);

        Assert.Equal("reason unavailable", failure.Error);
        Assert.StartsWith("the port can be opened now, so the failure was temporary", failure.Detail);
    }

    [Theory]
    [InlineData(SocketError.AddressAlreadyInUse, "AddressAlreadyInUse, 10048")]
    [InlineData(SocketError.AccessDenied, "AccessDenied, 10013")]
    [InlineData(SocketError.NetworkDown, "NetworkDown, 10050")]
    public void FormatError_UsesTheEnumNameAndItsFixedValue(SocketError error, string expected)
    {
        Assert.Equal(expected, UdpBindDiagnostics.FormatError(error));
    }

    [Fact]
    public void FormatError_WithoutAnError_IsReasonUnavailable()
    {
        Assert.Equal("reason unavailable", UdpBindDiagnostics.FormatError(null));
    }

    [Fact]
    public void Format_OpensNow_DoesNotClaimAHolder()
    {
        Assert.Equal(
            "the port can be opened now, so the failure was temporary (often a program that has since exited). " + Closing,
            UdpBindDiagnostics.Format(4200, null));
    }

    [Fact]
    public void Format_InUse_NamesTheLookupCommands()
    {
        Assert.Equal(
            "find the owner with \"netstat -ano -p udp\" (Windows) or \"ss -ulpn 'sport = :4200'\" (Linux and Wine hosts). " + Closing,
            UdpBindDiagnostics.Format(4200, SocketError.AddressAlreadyInUse));
    }

    [Fact]
    public void Format_AccessDenied_NamesEveryKnownCause()
    {
        string detail = UdpBindDiagnostics.Format(4200, SocketError.AccessDenied);

        Assert.StartsWith("the operating system refused the port: ", detail);
        Assert.Contains("netsh interface ipv4 show excludedportrange protocol=udp", detail);
        Assert.Contains("security software may have blocked this program", detail);
        Assert.Contains("on Linux ports below 1024 need extra privileges", detail);
        Assert.EndsWith(Closing, detail);
    }

    [Fact]
    public void Format_OtherError_OnlyCloses()
    {
        Assert.Equal(Closing, UdpBindDiagnostics.Format(4200, SocketError.NetworkDown));
    }
}
