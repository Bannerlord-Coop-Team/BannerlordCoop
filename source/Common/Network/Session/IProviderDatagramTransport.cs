using System;

namespace Common.Network.Session;

public enum ProviderConnectionState
{
    Connecting,
    Connected,
    Closed,
}

/// <summary>Provider SDK seam used by the shared loopback tunnel pumps.</summary>
public interface IProviderDatagramTransport : IDisposable
{
    PlatformIdentity LocalIdentity { get; }
    event Action<long, ProviderConnectionState> ConnectionStateChanged;

    void Prepare();
    long Connect(PlatformIdentity remoteIdentity, int channel);
    void Listen(int channel);
    void StopListening();
    void Accept(long connection);
    void Close(long connection);
    bool TryGetRemoteIdentity(long connection, out PlatformIdentity identity);
    bool Send(long connection, byte[] data, int length, bool droppable);
    int Receive(long connection, byte[] buffer);
    string Describe(long connection);
}

/// <summary>Shared framing and pump settings for every provider tunnel.</summary>
public static class ProviderTunnel
{
    public const int SessionChannel = 0;
    public const int MissionChannel = 1;
    public const int MaxDatagramBytes = 2048;
    public const int LoopbackBufferBytes = 2 * 1024 * 1024;
    public static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(2);

    public static bool IsDroppableDatagram(byte[] data, int length)
    {
        if (length < 1) return true;

        int property = data[0] & 0x1F;
        return property == 0 || property == 3 || property == 4;
    }
}
