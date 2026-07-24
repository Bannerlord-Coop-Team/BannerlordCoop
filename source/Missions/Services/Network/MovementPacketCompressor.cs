using Common.Logging;
using Common.PacketHandlers;
using Common.Serialization;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
using Serilog;
using System;
using System.Buffers;
using System.Diagnostics;

namespace Missions.Services.Network;

public interface IMovementPacketCompressor
{
    byte[] Serialize(IPacket packet);
    int GetSerializedLength(IPacket packet);
    bool TryRestore(IPacket packet, out IPacket restored);
}

public class MovementPacketCompressor : IMovementPacketCompressor
{
    internal const int MaxUncompressedBytes = 4096;
    private const double DiagnosticIntervalSeconds = 5;
    private static readonly ILogger Logger = LogManager.GetLogger<MovementPacketCompressor>();

    private readonly ICommonSerializer serializer;
    private readonly object diagnosticGate = new object();
    private long intervalStarted = Stopwatch.GetTimestamp();
    private long intervalOriginalBytes;
    private long intervalWireBytes;
    private int intervalPackets;

    public MovementPacketCompressor(ICommonSerializer serializer)
    {
        this.serializer = serializer;
    }

    public byte[] Serialize(IPacket packet)
    {
        return Serialize(packet, recordDiagnostic: true);
    }

    public int GetSerializedLength(IPacket packet)
    {
        return Serialize(packet, recordDiagnostic: false).Length;
    }

    private byte[] Serialize(IPacket packet, bool recordDiagnostic)
    {
        byte[] original = serializer.Serialize(packet);
        if (!IsMovement(packet))
            return original;

        if (original.Length == 0 ||
            original.Length > MaxUncompressedBytes)
        {
            if (recordDiagnostic)
                RecordDiagnostic(original.Length, original.Length);
            return original;
        }

        int maximumLength = LZ4Codec.MaximumOutputSize(original.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                original, 0, original.Length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength <= 0 || compressedLength >= original.Length)
            {
                if (recordDiagnostic)
                    RecordDiagnostic(original.Length, original.Length);
                return original;
            }

            var payload = new byte[compressedLength];
            Buffer.BlockCopy(buffer, 0, payload, 0, compressedLength);
            byte[] envelope = serializer.Serialize(
                new CompressedMovementPacket(original.Length, payload));
            byte[] wire = envelope.Length < original.Length ? envelope : original;
            if (recordDiagnostic)
                RecordDiagnostic(original.Length, wire.Length);
            return wire;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public bool TryRestore(IPacket packet, out IPacket restored)
    {
        if (!(packet is CompressedMovementPacket compressed))
        {
            restored = packet;
            return true;
        }

        restored = null;
        if (compressed.Payload == null ||
            compressed.UncompressedLength <= 0 ||
            compressed.UncompressedLength > MaxUncompressedBytes)
        {
            return false;
        }

        var decompressed = new byte[compressed.UncompressedLength];
        try
        {
            int decodedLength = LZ4Codec.Decode(
                compressed.Payload, 0, compressed.Payload.Length,
                decompressed, 0, decompressed.Length);
            if (decodedLength != decompressed.Length)
                return false;

            restored = serializer.Deserialize<IPacket>(decompressed);
            return restored != null && IsMovement(restored);
        }
        catch (Exception)
        {
            restored = null;
            return false;
        }
    }

    private static bool IsMovement(IPacket packet)
    {
        return packet.PacketType == PacketType.Movement ||
               packet.PacketType == PacketType.MountMovement;
    }

    private void RecordDiagnostic(int originalBytes, int wireBytes)
    {
        lock (diagnosticGate)
        {
            intervalPackets++;
            intervalOriginalBytes += originalBytes;
            intervalWireBytes += wireBytes;

            long now = Stopwatch.GetTimestamp();
            double seconds = (double)(now - intervalStarted) / Stopwatch.Frequency;
            if (seconds < DiagnosticIntervalSeconds)
                return;

            double savings = intervalOriginalBytes == 0
                ? 0
                : 1d - ((double)intervalWireBytes / intervalOriginalBytes);
            Logger.Information(
                "[BattleTraffic] Movement payloads: {Packets} packets, {OriginalBytes} -> {WireBytes} bytes " +
                "({Savings:P1} saved), {WireBytesPerSecond:F0} bytes/s per peer over {Seconds:F1}s",
                intervalPackets,
                intervalOriginalBytes,
                intervalWireBytes,
                savings,
                intervalWireBytes / seconds,
                seconds);

            intervalStarted = now;
            intervalOriginalBytes = 0;
            intervalWireBytes = 0;
            intervalPackets = 0;
        }
    }
}
