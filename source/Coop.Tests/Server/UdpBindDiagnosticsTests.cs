using Coop.Core.Server;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Xunit;

namespace Coop.Tests.Server;

/// <summary>Verifies the bind failure probe and the log text it produces.</summary>
public class UdpBindDiagnosticsTests
{
    private const string Closing = UdpBindDiagnostics.Closing;
    private const string CoopHint = ", a co-op server, probably one still running from an earlier session (its window may be hidden)";

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

    [Theory]
    [InlineData("powershell", "opened by powershell (Windows pid 1234).")]
    [InlineData("Bannerlord", "opened by Bannerlord (Windows pid 1234)" + CoopHint + ".")]
    [InlineData("bannerlordcoopserver", "opened by bannerlordcoopserver (Windows pid 1234)" + CoopHint + ".")]
    [InlineData("CustomLauncher", "opened by CustomLauncher (Windows pid 1234)" + CoopHint + ".")]
    [InlineData("dotnet", "opened by dotnet (Windows pid 1234).")]
    [InlineData("testhost", "opened by testhost (Windows pid 1234).")]
    [InlineData("a process with no readable name", "opened by a process with no readable name (Windows pid 1234).")]
    public void FormatOwners_NamesTheOwnerAndFlagsCoopServers(string name, string expected)
    {
        Assert.Equal(expected, UdpBindDiagnostics.FormatOwners(new[] { (1234, (string?)name) }, 99, "CustomLauncher"));
    }

    [Fact]
    public void FormatOwners_GenericHostIsNotCoopEvenAsTheCurrentProcessName()
    {
        Assert.Equal(
            "opened by dotnet (Windows pid 1234).",
            UdpBindDiagnostics.FormatOwners(new[] { (1234, (string?)"dotnet") }, 99, "dotnet"));
    }

    [Fact]
    public void FormatOwners_CurrentProcess_IsThisProcess()
    {
        Assert.Equal(
            "opened by this process (Windows pid 99).",
            UdpBindDiagnostics.FormatOwners(new[] { (99, (string?)"Bannerlord") }, 99, "Bannerlord"));
    }

    [Fact]
    public void FormatOwners_ExitedProcess_SaysAChildMayHoldThePort()
    {
        Assert.Equal(
            "opened by Windows pid 1234, which has exited; a program it started may still hold the port " +
            "(close it, or restart the PC).",
            UdpBindDiagnostics.FormatOwners(new[] { (1234, (string?)null) }, 99, "Bannerlord"));
    }

    [Fact]
    public void FormatOwners_MoreThanFive_CountsTheRest()
    {
        var owners = Enumerable.Range(1, 7).Select(pid => (pid, (string?)"svc")).ToArray();

        string text = UdpBindDiagnostics.FormatOwners(owners, 99, "Bannerlord");

        Assert.Equal(
            "opened by svc (Windows pid 1); svc (Windows pid 2); svc (Windows pid 3); svc (Windows pid 4); " +
            "svc (Windows pid 5) (and 2 more).",
            text);
    }

    [Fact]
    public void ParseOwnerPids_V4Table_ReturnsMatchingRowsOnly()
    {
        byte[] table = Table(12, Row(12, 4, 8, 4200, 1234), Row(12, 4, 8, 4201, 5678));

        Assert.Equal(new[] { 1234 }, UdpBindDiagnostics.ParseOwnerPids(table, 12, 4, 8, 4200));
    }

    [Fact]
    public void ParseOwnerPids_V6Table_ReadsTheV6Offsets()
    {
        byte[] table = Table(28, Row(28, 20, 24, 4200, 1234));

        Assert.Equal(new[] { 1234 }, UdpBindDiagnostics.ParseOwnerPids(table, 28, 20, 24, 4200));
    }

    [Fact]
    public void ParseOwnerPids_ReadsThePortInNetworkOrder()
    {
        // 4200 is 0x1068; a host-order read would see 0x6810.
        byte[] table = Table(12, Row(12, 4, 8, 0x6810, 1234));

        Assert.Empty(UdpBindDiagnostics.ParseOwnerPids(table, 12, 4, 8, 4200));
    }

    [Fact]
    public void ParseOwnerPids_IgnoresTheUpperPortBytes()
    {
        byte[] row = Row(12, 4, 8, 4200, 1234);
        row[6] = 0xAB;
        row[7] = 0xCD;

        Assert.Equal(new[] { 1234 }, UdpBindDiagnostics.ParseOwnerPids(Table(12, row), 12, 4, 8, 4200));
    }

    [Fact]
    public void ParseOwnerPids_CountLargerThanTheBuffer_ReadsOnlyWholeRows()
    {
        byte[] table = Table(12, Row(12, 4, 8, 4200, 1234));
        BitConverter.GetBytes(1000u).CopyTo(table, 0);
        Array.Resize(ref table, table.Length + 5);

        Assert.Equal(new[] { 1234 }, UdpBindDiagnostics.ParseOwnerPids(table, 12, 4, 8, 4200));
    }

    [Fact]
    public void ParseOwnerPids_SkipsPidZero()
    {
        byte[] table = Table(12, Row(12, 4, 8, 4200, 0), Row(12, 4, 8, 4200, 1234));

        Assert.Equal(new[] { 1234 }, UdpBindDiagnostics.ParseOwnerPids(table, 12, 4, 8, 4200));
    }

    [Fact]
    public void ParseOwnerPids_EmptyOrShortTables_ReturnNothing()
    {
        Assert.Empty(UdpBindDiagnostics.ParseOwnerPids(null, 12, 4, 8, 4200));
        Assert.Empty(UdpBindDiagnostics.ParseOwnerPids(new byte[3], 12, 4, 8, 4200));
        Assert.Empty(UdpBindDiagnostics.ParseOwnerPids(Table(12), 12, 4, 8, 4200));
    }

    [Fact]
    public void FindOwnerPids_OnWindows_FindsThisProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)holder.LocalEndPoint!).Port;

        Assert.Contains(Environment.ProcessId, UdpBindDiagnostics.FindOwnerPids(port));
    }

    [Fact]
    public void FindOwnerPids_OffWindows_IsEmpty()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        using var holder = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        holder.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)holder.LocalEndPoint!).Port;

        Assert.Empty(UdpBindDiagnostics.FindOwnerPids(port));
    }

    private static byte[] Row(int rowSize, int portOffset, int pidOffset, int port, int pid)
    {
        var row = new byte[rowSize];
        row[portOffset] = (byte)(port >> 8);
        row[portOffset + 1] = (byte)port;
        BitConverter.GetBytes(pid).CopyTo(row, pidOffset);
        return row;
    }

    private static byte[] Table(int rowSize, params byte[][] rows)
    {
        var table = new byte[4 + (rows.Length * rowSize)];
        BitConverter.GetBytes((uint)rows.Length).CopyTo(table, 0);
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].CopyTo(table, 4 + (i * rowSize));
        }

        return table;
    }
}
