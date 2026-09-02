using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerWireManifestTests
{
    [Fact]
    public void Manifest_HasFrozenTypeIdsAndTransportLanes()
    {
        var expected = new[]
        {
            ("Coop.Core.Common.Network.Packets.CampaignTimePacket", 488231864, (byte)0, "Sequenced"),
            ("Coop.Core.Server.Connections.Messages.NetworkModuleVersionsValidate", 1457133576, (byte)0, "ReliableOrdered"),
            ("Coop.Core.Server.Connections.Messages.NetworkModuleVersionsValidated", 1206877260, (byte)0, "ReliableOrdered"),
            ("Coop.Core.Server.Connections.Messages.NetworkClientValidate", 791628818, (byte)0, "ReliableOrdered"),
            ("Coop.Core.Server.Connections.Messages.NetworkClientValidated", 29530214, (byte)0, "ReliableOrdered"),
            ("Coop.Core.Common.Session.Messages.NetworkSessionLobbyChanged", 1547717120, (byte)0, "ReliableOrdered"),
            ("Common.PacketHandlers.AggregateMessagePacket", 1253361833, (byte)0, "ReliableOrdered"),
            ("Coop.Core.Common.Network.Packets.GameSaveDataChunkPacket", 404232623, (byte)1, "ReliableOrdered")
        };

        Assert.Equal(expected.Length, DedicatedServerWireManifest.Entries.Count);
        foreach ((string fullTypeName, int typeId, byte channel, string deliveryMethod) in expected)
        {
            DedicatedServerWireEntry entry = Assert.Single(
                DedicatedServerWireManifest.Entries,
                item => item.FullTypeName == fullTypeName);
            Assert.Equal(typeId, entry.TypeId);
            Assert.Equal(typeId, DedicatedServerWireManifest.ComputeTypeId(fullTypeName));
            Assert.Equal(channel, entry.Channel);
            Assert.Equal(deliveryMethod, entry.DeliveryMethod);
        }

        Assert.True(DedicatedServerWireManifest.GetByTypeId(
            DedicatedServerWireManifest.GameSaveDataChunkPacketTypeId).Optional);
    }

    [Fact]
    public void Manifest_HashIsGolden()
    {
        Assert.Equal(
            "6d880eb4ea330e9aa1f43037c1850543b08aaab70e28151793b22044e165a815",
            DedicatedServerWireManifest.Sha256);
    }

    [Fact]
    public void Manifest_UnknownTypeFailsClosed()
    {
        Assert.Throws<InvalidDataException>(() => DedicatedServerWireManifest.GetByTypeId(42));
    }
}
