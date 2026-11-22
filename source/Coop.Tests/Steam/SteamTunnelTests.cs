using Coop.Steam;
using System.Linq;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamTunnelTests
    {
        // LiteNetLib 1.3.1 Merged layout: header byte (property 12), then [ushort size][packet] per packet.
        internal static readonly byte[] MergedUnreliableAndChanneled =
        {
            12,
            2, 0, 0, 9,
            4, 0, 1, 0, 0, 7,
        };

        internal static readonly byte[] MergedUnreliable = { 12, 2, 0, 0, 9 };

        internal static readonly byte[] MergedChanneled = { 12, 4, 0, 1, 0, 0, 7 };

        [Fact]
        public void SplitByLane_MergedDatagram_SeparatesUnreliablePacketsFromReliableOnes()
        {
            var (droppable, reliable) = Split(MergedUnreliableAndChanneled);

            Assert.Equal(MergedUnreliable, droppable);
            Assert.Equal(MergedChanneled, reliable);
        }

        [Fact]
        public void SplitByLane_MergedDatagram_KeepsHeaderByteAndPacketOrderPerLane()
        {
            // Connection number bits (0x40) ride in every header next to the packet property.
            var merged = new byte[]
            {
                12 | 0x40,
                4, 0, 1 | 0x40, 2, 0, 1,
                2, 0, 0x40, 2,
                5, 0, 1 | 0x40, 2, 1, 0, 3,
                2, 0, 0x40, 4,
            };

            var (droppable, reliable) = Split(merged);

            Assert.Equal(new byte[] { 12 | 0x40, 2, 0, 0x40, 2, 2, 0, 0x40, 4 }, droppable);
            Assert.Equal(
                new byte[] { 12 | 0x40, 4, 0, 1 | 0x40, 2, 0, 1, 5, 0, 1 | 0x40, 2, 1, 0, 3 },
                reliable);
        }

        [Fact]
        public void SplitByLane_MergedDatagramOfOneLane_LeavesTheOtherLaneEmpty()
        {
            var (droppable, reliable) = Split(new byte[] { 12, 2, 0, 0, 9, 2, 0, 0, 8 });

            Assert.Equal(new byte[] { 12, 2, 0, 0, 9, 2, 0, 0, 8 }, droppable);
            Assert.Empty(reliable);
        }

        [Theory]
        [InlineData(new byte[] { 1, 0, 0, 7 })]
        [InlineData(new byte[] { 2, 0, 0, 1 })]
        [InlineData(new byte[] { 5, 1, 2 })]
        public void SplitByLane_ReliableClassDatagram_GoesWholeOnTheReliableLane(byte[] datagram)
        {
            var (droppable, reliable) = Split(datagram);

            Assert.Empty(droppable);
            Assert.Equal(datagram, reliable);
        }

        [Theory]
        [InlineData(new byte[] { 0, 9 })]
        [InlineData(new byte[] { 3, 0, 1 })]
        [InlineData(new byte[] { 4, 0, 1 })]
        public void SplitByLane_DroppableDatagram_GoesWholeOnTheDroppableLane(byte[] datagram)
        {
            var (droppable, reliable) = Split(datagram);

            Assert.Equal(datagram, droppable);
            Assert.Empty(reliable);
        }

        [Theory]
        [InlineData(new byte[] { 12, 2, 0, 0, 9, 5, 0, 1 })]
        [InlineData(new byte[] { 12, 2, 0, 0, 9, 0, 0 })]
        [InlineData(new byte[] { 12, 2, 0, 0, 9, 4 })]
        public void SplitByLane_MalformedMergedDatagram_GoesWholeOnTheReliableLane(byte[] datagram)
        {
            var (droppable, reliable) = Split(datagram);

            Assert.Empty(droppable);
            Assert.Equal(datagram, reliable);
        }

        private static (byte[] Droppable, byte[] Reliable) Split(byte[] datagram)
        {
            var droppable = new byte[SteamTunnel.MaxDatagramBytes];
            var reliable = new byte[SteamTunnel.MaxDatagramBytes];
            SteamTunnel.SplitByLane(datagram, datagram.Length, droppable, out int droppableLength,
                reliable, out int reliableLength);
            return (droppable.Take(droppableLength).ToArray(), reliable.Take(reliableLength).ToArray());
        }
    }
}
