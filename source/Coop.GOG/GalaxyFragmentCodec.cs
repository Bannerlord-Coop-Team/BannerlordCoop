using System;
using System.Collections.Generic;

namespace Coop.GOG;

internal readonly struct GalaxyFragment
{
    public GalaxyFragment(
        uint messageId,
        ushort index,
        ushort count,
        int totalLength,
        bool close,
        byte[] payload)
    {
        MessageId = messageId;
        Index = index;
        Count = count;
        TotalLength = totalLength;
        Close = close;
        Payload = payload;
    }

    public uint MessageId { get; }
    public ushort Index { get; }
    public ushort Count { get; }
    public int TotalLength { get; }
    public bool Close { get; }
    public byte[] Payload { get; }
}

/// <summary>Frames LiteNetLib datagrams into bounded Galaxy P2P packets.</summary>
internal static class GalaxyFragmentCodec
{
    public const int MaxPacketBytes = 1024;
    public const int HeaderBytes = 18;
    internal const int MaxPayloadBytes = MaxPacketBytes - HeaderBytes;
    private const int MaxDatagramBytes = 2048;
    private const int MaxFragmentCount =
        (MaxDatagramBytes + MaxPayloadBytes - 1) / MaxPayloadBytes;
    private const byte Version = 1;
    private const byte CloseFlag = 1;

    public static IReadOnlyList<byte[]> Encode(uint messageId, byte[] data, int length)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (length < 0 || length > data.Length || length > MaxDatagramBytes)
            throw new ArgumentOutOfRangeException(nameof(length));

        int fragmentCount = Math.Max(1, (length + MaxPayloadBytes - 1) / MaxPayloadBytes);
        var packets = new byte[fragmentCount][];
        int offset = 0;
        for (int index = 0; index < fragmentCount; index++)
        {
            int payloadLength = Math.Min(MaxPayloadBytes, length - offset);
            var packet = new byte[HeaderBytes + payloadLength];
            WriteHeader(
                packet,
                messageId,
                checked((ushort)index),
                checked((ushort)fragmentCount),
                length,
                flags: 0);
            if (payloadLength > 0)
                Array.Copy(data, offset, packet, HeaderBytes, payloadLength);
            packets[index] = packet;
            offset += payloadLength;
        }

        return packets;
    }

    public static byte[] EncodeClose(uint messageId)
    {
        var packet = new byte[HeaderBytes];
        WriteHeader(packet, messageId, 0, 1, 0, CloseFlag);
        return packet;
    }

    public static bool TryDecode(byte[] packet, out GalaxyFragment fragment)
    {
        fragment = default;
        if (packet == null || packet.Length < HeaderBytes || packet.Length > MaxPacketBytes)
            return false;
        if (packet[0] != (byte)'B' || packet[1] != (byte)'C' ||
            packet[2] != (byte)'G' || packet[3] != (byte)'F' ||
            packet[4] != Version)
        {
            return false;
        }

        byte flags = packet[5];
        if ((flags & ~CloseFlag) != 0) return false;

        uint messageId = ReadUInt32(packet, 6);
        ushort index = ReadUInt16(packet, 10);
        ushort count = ReadUInt16(packet, 12);
        int totalLength = checked((int)ReadUInt32(packet, 14));
        bool close = (flags & CloseFlag) != 0;

        if (count == 0 || count > MaxFragmentCount || index >= count ||
            totalLength < 0 || totalLength > MaxDatagramBytes)
            return false;
        if (close && (totalLength != 0 || count != 1 || index != 0 || packet.Length != HeaderBytes))
            return false;

        int payloadLength = packet.Length - HeaderBytes;
        if (!close)
        {
            int expectedCount = Math.Max(1,
                (totalLength + MaxPayloadBytes - 1) / MaxPayloadBytes);
            int expectedPayloadLength = Math.Min(
                MaxPayloadBytes,
                totalLength - (index * MaxPayloadBytes));
            if (count != expectedCount || payloadLength != expectedPayloadLength)
                return false;
        }

        var payload = new byte[payloadLength];
        if (payloadLength > 0)
            Array.Copy(packet, HeaderBytes, payload, 0, payloadLength);
        fragment = new GalaxyFragment(messageId, index, count, totalLength, close, payload);
        return true;
    }

    private static void WriteHeader(
        byte[] packet,
        uint messageId,
        ushort index,
        ushort count,
        int totalLength,
        byte flags)
    {
        packet[0] = (byte)'B';
        packet[1] = (byte)'C';
        packet[2] = (byte)'G';
        packet[3] = (byte)'F';
        packet[4] = Version;
        packet[5] = flags;
        WriteUInt32(packet, 6, messageId);
        WriteUInt16(packet, 10, index);
        WriteUInt16(packet, 12, count);
        WriteUInt32(packet, 14, checked((uint)totalLength));
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static uint ReadUInt32(byte[] data, int offset) =>
        (uint)(data[offset] |
            (data[offset + 1] << 8) |
            (data[offset + 2] << 16) |
            (data[offset + 3] << 24));

    private static void WriteUInt16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }
}

/// <summary>Reassembles bounded, out-of-order Galaxy fragments by message id.</summary>
internal sealed class GalaxyFragmentReassembler
{
    private const int MaxIncompleteMessages = 64;

    private sealed class PendingMessage
    {
        public int TotalLength;
        public byte[][] Fragments;
        public int Received;
        public LinkedListNode<uint> InsertionNode;
    }

    private readonly Dictionary<uint, PendingMessage> pending = new Dictionary<uint, PendingMessage>();
    private readonly LinkedList<uint> insertionOrder = new LinkedList<uint>();

    public bool TryAdd(GalaxyFragment fragment, out byte[] datagram)
    {
        datagram = null;
        if (fragment.Close) return false;

        if (!pending.TryGetValue(fragment.MessageId, out var message))
        {
            if (pending.Count >= MaxIncompleteMessages && insertionOrder.First != null)
                RemovePending(insertionOrder.First.Value);

            message = new PendingMessage
            {
                TotalLength = fragment.TotalLength,
                Fragments = new byte[fragment.Count][],
            };
            message.InsertionNode = insertionOrder.AddLast(fragment.MessageId);
            pending.Add(fragment.MessageId, message);
        }

        if (message.TotalLength != fragment.TotalLength ||
            message.Fragments.Length != fragment.Count)
        {
            RemovePending(fragment.MessageId);
            return false;
        }

        if (message.Fragments[fragment.Index] == null)
        {
            message.Fragments[fragment.Index] = fragment.Payload;
            message.Received++;
        }

        if (message.Received != message.Fragments.Length) return false;

        datagram = new byte[message.TotalLength];
        int offset = 0;
        foreach (byte[] payload in message.Fragments)
        {
            if (payload == null || offset + payload.Length > datagram.Length)
            {
                datagram = null;
                RemovePending(fragment.MessageId);
                return false;
            }

            Array.Copy(payload, 0, datagram, offset, payload.Length);
            offset += payload.Length;
        }

        RemovePending(fragment.MessageId);
        if (offset != datagram.Length)
        {
            datagram = null;
            return false;
        }

        return true;
    }

    private void RemovePending(uint messageId)
    {
        if (!pending.TryGetValue(messageId, out var message)) return;

        pending.Remove(messageId);
        insertionOrder.Remove(message.InsertionNode);
    }
}
