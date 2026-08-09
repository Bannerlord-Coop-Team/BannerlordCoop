using Coop.GOG;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxyFragmentCodecTests
{
    [Fact]
    public void EncodeAndReassemble_MaxDatagramSupportsDuplicateAndOutOfOrderFragments()
    {
        byte[] source = Enumerable.Range(0, 2048).Select(index => (byte)(index % 251)).ToArray();
        var packets = GalaxyFragmentCodec.Encode(17, source, source.Length);
        Assert.Equal(3, packets.Count);
        var fragments = packets.Select(Decode).ToArray();
        var reassembler = new GalaxyFragmentReassembler();

        Assert.False(reassembler.TryAdd(fragments[2], out _));
        Assert.False(reassembler.TryAdd(fragments[0], out _));
        Assert.False(reassembler.TryAdd(fragments[0], out _));
        Assert.True(reassembler.TryAdd(fragments[1], out byte[] actual));

        Assert.Equal(source, actual);
    }

    [Fact]
    public void TryDecode_CloseFrameIsDistinctFromEmptyDatagram()
    {
        Assert.True(GalaxyFragmentCodec.TryDecode(
            GalaxyFragmentCodec.EncodeClose(9),
            out var close));
        Assert.True(close.Close);

        byte[] emptyPacket = Assert.Single(GalaxyFragmentCodec.Encode(10, Array.Empty<byte>(), 0));
        Assert.True(GalaxyFragmentCodec.TryDecode(emptyPacket, out var empty));
        Assert.False(empty.Close);
        var reassembler = new GalaxyFragmentReassembler();
        Assert.True(reassembler.TryAdd(empty, out byte[] datagram));
        Assert.Empty(datagram);
    }

    [Fact]
    public void TryDecode_RejectsUnknownFlagsAndUnboundedFragmentCounts()
    {
        byte[] unknownFlags = GalaxyFragmentCodec.Encode(1, new byte[] { 1 }, 1)[0];
        unknownFlags[5] = 2;
        Assert.False(GalaxyFragmentCodec.TryDecode(unknownFlags, out _));

        byte[] excessiveCount = GalaxyFragmentCodec.Encode(2, new byte[] { 1 }, 1)[0];
        excessiveCount[12] = byte.MaxValue;
        excessiveCount[13] = byte.MaxValue;
        Assert.False(GalaxyFragmentCodec.TryDecode(excessiveCount, out _));
    }

    [Fact]
    public void TryDecode_RejectsFragmentShapeThatCannotMatchDeclaredDatagram()
    {
        byte[] packet = GalaxyFragmentCodec.Encode(1, new byte[20], 20)[0];
        Array.Resize(ref packet, packet.Length - 1);

        Assert.False(GalaxyFragmentCodec.TryDecode(packet, out _));
    }

    [Fact]
    public void Reassembler_EvictsOldestIncompleteMessageAtBound()
    {
        byte[] source = new byte[GalaxyFragmentCodec.MaxPayloadBytes + 1];
        var reassembler = new GalaxyFragmentReassembler();
        GalaxyFragment[] newest = null;

        for (uint messageId = 1; messageId <= 65; messageId++)
        {
            GalaxyFragment[] fragments = GalaxyFragmentCodec.Encode(messageId, source, source.Length)
                .Select(Decode)
                .ToArray();
            Assert.False(reassembler.TryAdd(fragments[0], out _));
            if (messageId == 1) newest = fragments;
            if (messageId == 65) newest = fragments;
        }

        GalaxyFragment[] oldest = GalaxyFragmentCodec.Encode(1, source, source.Length)
            .Select(Decode)
            .ToArray();
        Assert.False(reassembler.TryAdd(oldest[1], out _));
        Assert.True(reassembler.TryAdd(newest[1], out byte[] completed));
        Assert.Equal(source, completed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2049)]
    public void Encode_RejectsOutOfBoundsLength(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GalaxyFragmentCodec.Encode(1, new byte[2049], length));
    }

    private static GalaxyFragment Decode(byte[] packet)
    {
        Assert.True(GalaxyFragmentCodec.TryDecode(packet, out var fragment));
        return fragment;
    }
}
