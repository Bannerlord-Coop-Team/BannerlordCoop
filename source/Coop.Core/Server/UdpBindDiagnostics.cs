using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Coop.Core.Server;

/// <summary>
/// Explains why the server could not bind its UDP port and, on Windows, which processes opened it,
/// for the bind failure log line.
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
    private const string NameUnavailable = "a process with no readable name";
    private const int MaxOwners = 5;

    private static readonly string[] CoopServerNames = { "Bannerlord", "BannerlordCoopServer" };
    // Generic hosts that also run tests and tools, so a name match says nothing about a co-op server.
    private static readonly string[] GenericHostNames = { "dotnet", "testhost" };

    public UdpBindFailure Describe(int port)
    {
        try
        {
            if (port <= 0) return new UdpBindFailure(ReasonUnavailable, Closing);

            SocketError? error = Probe(port);
            // Only AddressAlreadyInUse can mean a holder; AccessDenied never does for an exclusive wildcard bind.
            string? owners = error == SocketError.AddressAlreadyInUse ? DescribeOwners(port) : null;
            string detail = owners == null ? Format(port, error) : owners + " " + Closing;
            return new UdpBindFailure(FormatError(error), detail);
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

    // The table keeps the pid that bound the socket. A child can inherit the socket after that process exits,
    // and the pid can be reused, hence "opened by".
    private static string? DescribeOwners(int port)
    {
        try
        {
            IReadOnlyList<int> pids = FindOwnerPids(port);
            if (pids.Count == 0) return null;

            using var current = Process.GetCurrentProcess();
            return FormatOwners(pids.Select(NameOwner).ToArray(), current.Id, current.ProcessName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static IReadOnlyList<int> FindOwnerPids(int port)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Array.Empty<int>();

        // MIB_UDPROW_OWNER_PID is 12 bytes (port at 4, pid at 8), MIB_UDP6ROW_OWNER_PID is 28 (port at 20, pid at 24).
        return ParseOwnerPids(ReadUdpTable(AfInet), 12, 4, 8, port)
            .Concat(ParseOwnerPids(ReadUdpTable(AfInet6), 28, 20, 24, port))
            .Distinct()
            .ToArray();
    }

    // Copied into managed memory, so a bad row count cannot read past the native buffer.
    private static byte[]? ReadUdpTable(int addressFamily)
    {
        try
        {
            int size = 0;
            uint result = GetExtendedUdpTable(IntPtr.Zero, ref size, false, addressFamily, UdpTableOwnerPid, 0);
            // The table can grow between the size query and the copy.
            for (int attempt = 0; attempt < 3 && result == ErrorInsufficientBuffer && size > 0; attempt++)
            {
                int allocated = size;
                IntPtr buffer = Marshal.AllocHGlobal(allocated);
                try
                {
                    result = GetExtendedUdpTable(buffer, ref size, false, addressFamily, UdpTableOwnerPid, 0);
                    if (result == 0)
                    {
                        var table = new byte[allocated];
                        Marshal.Copy(buffer, table, 0, allocated);
                        return table;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch (Exception)
        {
            // DllNotFound or EntryPointNotFound on a runtime without iphlpapi; the command hint covers it.
        }

        return null;
    }

    // Each row holds the port in network order in the low two bytes of a DWORD whose upper bytes are undefined.
    internal static IReadOnlyList<int> ParseOwnerPids(byte[]? table, int rowSize, int portOffset, int pidOffset, int port)
    {
        var pids = new List<int>();
        if (table == null || table.Length < 4) return pids;

        long rows = Math.Min(BitConverter.ToUInt32(table, 0), (table.Length - 4) / rowSize);
        for (int row = 0; row < rows; row++)
        {
            int start = 4 + (row * rowSize);
            int rowPort = (table[start + portOffset] << 8) | table[start + portOffset + 1];
            int pid = BitConverter.ToInt32(table, start + pidOffset);
            if (rowPort == port && pid != 0) pids.Add(pid);
        }

        return pids;
    }

    private static (int Pid, string? Name) NameOwner(int pid)
    {
        try
        {
            // ProcessName only; MainModule holds a path with the user name.
            using var process = Process.GetProcessById(pid);
            return (pid, process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            return (pid, null);
        }
        catch (Exception)
        {
            return (pid, NameUnavailable);
        }
    }

    // Owners are distinct pids, each with its process name, or null when the process has exited.
    internal static string FormatOwners(IReadOnlyList<(int Pid, string? Name)> owners, int currentPid, string currentName)
    {
        IEnumerable<string> shown = owners
            .Take(MaxOwners)
            .Select(owner => DescribeOwner(owner.Pid, owner.Name, currentPid, currentName));
        string more = owners.Count > MaxOwners ? $" (and {owners.Count - MaxOwners} more)" : "";
        return "opened by " + string.Join("; ", shown) + more + ".";
    }

    // "Windows pid" because Wine reports its own process ids, not Unix ones.
    private static string DescribeOwner(int pid, string? name, int currentPid, string currentName)
    {
        if (pid == currentPid) return $"this process (Windows pid {pid})";
        if (name == null)
        {
            return $"Windows pid {pid}, which has exited; a program it started may still hold the port (close it, or restart the PC)";
        }

        string owner = $"{name} (Windows pid {pid})";
        return IsCoopServer(name, currentName)
            ? owner + ", a co-op server, probably one still running from an earlier session (its window may be hidden)"
            : owner;
    }

    private static bool IsCoopServer(string name, string currentName)
    {
        if (GenericHostNames.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;

        return CoopServerNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase);
    }

    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int UdpTableOwnerPid = 1;
    private const uint ErrorInsufficientBuffer = 122;

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedUdpTable(
        IntPtr table, ref int size, bool order, int addressFamily, int tableClass, int reserved);
}
