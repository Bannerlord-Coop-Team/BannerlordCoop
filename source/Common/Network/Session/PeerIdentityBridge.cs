using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Common.Network.Session;

/// <summary>Publishes provider-authenticated loopback peer mappings to a separate server process.</summary>
public interface IPeerIdentityPublisher : IDisposable
{
    bool IsAvailable { get; }
    bool TryRegister(IPEndPoint serverPeerEndpoint, PlatformIdentity identity);
    void Unregister(IPEndPoint serverPeerEndpoint);
    void UnregisterAll();
}

/// <summary>Supplies the local UDP endpoint seen by the authoritative server.</summary>
public interface ILocalPeerEndpointSource
{
    IPEndPoint LocalPeerEndpoint { get; }
}

/// <summary>Creates and validates unguessable per-session bridge names.</summary>
public static class PeerIdentityBridgeName
{
    private const string Prefix = "bannerlordcoop-peer-identity-";

    public static string Create() => Prefix + Guid.NewGuid().ToString("N");

    public static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        return Guid.TryParseExact(value.Substring(Prefix.Length), "N", out _);
    }
}

/// <summary>No-op bridge used when both the provider tunnel and server live in one process.</summary>
public sealed class NoopPeerIdentityPublisher : IPeerIdentityPublisher
{
    public static readonly NoopPeerIdentityPublisher Instance = new NoopPeerIdentityPublisher();

    public bool IsAvailable => false;
    public bool TryRegister(IPEndPoint serverPeerEndpoint, PlatformIdentity identity) => true;
    public void Unregister(IPEndPoint serverPeerEndpoint) { }
    public void UnregisterAll() { }
    public void Dispose() { }
}

/// <summary>Client side of the authenticated loopback identity bridge.</summary>
public sealed class NamedPipePeerIdentityPublisher : IPeerIdentityPublisher
{
    private const int ConnectTimeoutMilliseconds = 2000;
    private const int DisposeTimeoutMilliseconds = 250;

    private readonly string pipeName;
    private readonly int connectTimeoutMilliseconds;
    private readonly int disposeTimeoutMilliseconds;
    private readonly object gate = new object();
    private readonly HashSet<int> registeredPorts = new HashSet<int>();
    private volatile bool disposed;

    public NamedPipePeerIdentityPublisher(string pipeName)
        : this(pipeName, ConnectTimeoutMilliseconds, DisposeTimeoutMilliseconds)
    {
    }

    internal NamedPipePeerIdentityPublisher(
        string pipeName,
        int connectTimeoutMilliseconds,
        int disposeTimeoutMilliseconds)
    {
        if (!PeerIdentityBridgeName.IsValid(pipeName))
            throw new ArgumentException("A valid peer identity bridge name is required", nameof(pipeName));
        if (connectTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(connectTimeoutMilliseconds));
        if (disposeTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(disposeTimeoutMilliseconds));

        this.pipeName = pipeName;
        this.connectTimeoutMilliseconds = connectTimeoutMilliseconds;
        this.disposeTimeoutMilliseconds = disposeTimeoutMilliseconds;
    }

    public bool IsAvailable => !disposed;

    public bool TryRegister(IPEndPoint serverPeerEndpoint, PlatformIdentity identity)
    {
        if (!IsValidEndpoint(serverPeerEndpoint) ||
            !identity.IsValid ||
            !identity.IsStorefrontIdentity)
        {
            return false;
        }

        lock (gate)
        {
            if (disposed || !Send(
                    PeerIdentityBridgeProtocol.Register,
                    serverPeerEndpoint.Port,
                    identity,
                    connectTimeoutMilliseconds))
                return false;

            registeredPorts.Add(serverPeerEndpoint.Port);
            return true;
        }
    }

    public void Unregister(IPEndPoint serverPeerEndpoint)
    {
        if (!IsValidEndpoint(serverPeerEndpoint)) return;

        lock (gate)
        {
            if (!registeredPorts.Contains(serverPeerEndpoint.Port) || disposed) return;
            if (Send(
                    PeerIdentityBridgeProtocol.Unregister,
                    serverPeerEndpoint.Port,
                    default,
                    connectTimeoutMilliseconds))
                registeredPorts.Remove(serverPeerEndpoint.Port);
        }
    }

    public void UnregisterAll()
    {
        lock (gate)
        {
            if (registeredPorts.Count == 0 || disposed) return;
            if (Send(
                    PeerIdentityBridgeProtocol.UnregisterAll,
                    0,
                    default,
                    disposeTimeoutMilliseconds))
            {
                registeredPorts.Clear();
            }
        }
    }

    public void Dispose()
    {
        bool hasRegistrations;
        lock (gate)
        {
            if (disposed) return;

            hasRegistrations = registeredPorts.Count > 0;
            registeredPorts.Clear();
            disposed = true;
        }

        if (hasRegistrations)
            Send(
                PeerIdentityBridgeProtocol.UnregisterAll,
                0,
                default,
                disposeTimeoutMilliseconds);
    }

    private bool Send(
        byte operation,
        int port,
        PlatformIdentity identity,
        int timeoutMilliseconds)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect(timeoutMilliseconds);

            using var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);
            using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
            writer.Write(PeerIdentityBridgeProtocol.Magic);
            writer.Write(PeerIdentityBridgeProtocol.Version);
            writer.Write(operation);
            writer.Write(port);
            PeerIdentityBridgeProtocol.WriteString(writer, identity.Provider);
            PeerIdentityBridgeProtocol.WriteString(writer, identity.UserId);
            writer.Flush();

            return reader.ReadByte() == PeerIdentityBridgeProtocol.Accepted;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsValidEndpoint(IPEndPoint endpoint) =>
        endpoint != null &&
        IPAddress.Loopback.Equals(endpoint.Address) &&
        endpoint.Port >= IPEndPoint.MinPort &&
        endpoint.Port <= IPEndPoint.MaxPort;
}

/// <summary>Server side of the per-session loopback identity bridge.</summary>
public sealed class NamedPipePeerIdentityResolver : IAuthenticatedPeerIdentityResolver, IDisposable
{
    private readonly string pipeName;
    private readonly object gate = new object();
    private readonly Dictionary<int, PlatformIdentity> identities =
        new Dictionary<int, PlatformIdentity>();
    private readonly Thread listenerThread;

    private NamedPipeServerStream activePipe;
    private volatile bool disposed;

    public NamedPipePeerIdentityResolver(string pipeName)
    {
        if (!PeerIdentityBridgeName.IsValid(pipeName))
            throw new ArgumentException("A valid peer identity bridge name is required", nameof(pipeName));

        this.pipeName = pipeName;
        listenerThread = new Thread(Listen)
        {
            IsBackground = true,
            Name = "Coop peer identity bridge",
        };
        listenerThread.Start();
    }

    public bool TryGetIdentity(IPEndPoint serverPeerEndpoint, out PlatformIdentity identity)
    {
        lock (gate)
        {
            if (serverPeerEndpoint != null &&
                IPAddress.Loopback.Equals(serverPeerEndpoint.Address))
                return identities.TryGetValue(serverPeerEndpoint.Port, out identity);

            identity = default;
            return false;
        }
    }

    private void Listen()
    {
        while (!IsDisposed())
        {
            try
            {
                using (var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None))
                {
                    SetActivePipe(pipe);
                    while (!IsDisposed())
                    {
                        bool connectionAccepted = false;
                        try
                        {
                            pipe.WaitForConnection();
                            connectionAccepted = true;
                            Handle(pipe);
                        }
                        finally
                        {
                            if (connectionAccepted && !IsDisposed())
                                pipe.Disconnect();
                        }
                    }
                }
            }
            catch (SocketException) when (IsDisposed())
            {
            }
            catch (Exception exception) when (
                exception is SocketException ||
                exception is IOException ||
                exception is ObjectDisposedException ||
                exception is InvalidOperationException)
            {
                if (!IsDisposed()) Thread.Sleep(25);
            }
            finally
            {
                ClearActivePipe();
            }
        }
    }

    private void SetActivePipe(NamedPipeServerStream pipe)
    {
        lock (gate)
        {
            if (disposed)
            {
                pipe.Dispose();
                return;
            }

            activePipe = pipe;
        }
    }

    private void ClearActivePipe()
    {
        lock (gate)
        {
            activePipe = null;
        }
    }

    private bool IsDisposed()
    {
        lock (gate)
        {
            return disposed;
        }
    }

    private void Handle(Stream pipe)
    {
        using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);

        bool accepted = reader.ReadInt32() == PeerIdentityBridgeProtocol.Magic &&
            reader.ReadByte() == PeerIdentityBridgeProtocol.Version;
        byte operation = reader.ReadByte();
        int port = reader.ReadInt32();
        string provider = PeerIdentityBridgeProtocol.ReadString(reader);
        string userId = PeerIdentityBridgeProtocol.ReadString(reader);

        accepted &= operation == PeerIdentityBridgeProtocol.UnregisterAll ||
            (port >= IPEndPoint.MinPort && port <= IPEndPoint.MaxPort);
        var identity = new PlatformIdentity(provider, userId);

        lock (gate)
        {
            if (disposed)
            {
                accepted = false;
            }
            else if (accepted && operation == PeerIdentityBridgeProtocol.Register &&
                identity.IsValid && identity.IsStorefrontIdentity)
            {
                identities[port] = identity;
            }
            else if (accepted && operation == PeerIdentityBridgeProtocol.Unregister)
            {
                identities.Remove(port);
            }
            else if (accepted && operation == PeerIdentityBridgeProtocol.UnregisterAll)
            {
                identities.Clear();
            }
            else
            {
                accepted = false;
            }
        }

        writer.Write(accepted
            ? PeerIdentityBridgeProtocol.Accepted
            : PeerIdentityBridgeProtocol.Rejected);
        writer.Flush();
    }

    public void Dispose()
    {
        NamedPipeServerStream pipe;
        lock (gate)
        {
            if (disposed) return;

            disposed = true;
            identities.Clear();
            pipe = activePipe;
        }

        pipe?.Dispose();
        listenerThread.Join(TimeSpan.FromSeconds(2));
    }
}

internal static class PeerIdentityBridgeProtocol
{
    public const int Magic = 0x42434950;
    public const byte Version = 2;
    public const byte Register = 1;
    public const byte Unregister = 2;
    public const byte UnregisterAll = 3;
    public const byte Accepted = 1;
    public const byte Rejected = 0;

    private const int MaxStringBytes = 256;

    public static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (bytes.Length > MaxStringBytes)
            throw new ArgumentOutOfRangeException(nameof(value));

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > MaxStringBytes)
            throw new InvalidDataException("Peer identity bridge string is out of range");

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
            throw new EndOfStreamException();

        return Encoding.UTF8.GetString(bytes);
    }
}
