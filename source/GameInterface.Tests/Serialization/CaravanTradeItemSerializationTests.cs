using GameInterface.Services.Caravans.Messages;
using GameInterface.Surrogates;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>Checks that caravan trade messages retain unresolved item identities.</summary>
public class CaravanTradeItemSerializationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("unregistered-caravan-modifier")]
    public async Task Receive_PreservesUnknownItemsAndHistoricalWireShape(string? modifierId)
    {
        _ = new SurrogateCollection();
        var original = new HistoricalMessage
        {
            PartyId = "caravan-party",
            Rows = new List<HistoricalRow>
            {
                new HistoricalRow
                {
                    BoughtSettlementId = "town_ES1",
                    BuyPrice = 14,
                    SellPrice = 19,
                    Item = new ItemRosterElementSurrogate
                    {
                        ItemObjectId = "unregistered-caravan-item",
                        Amount = 7,
                        ItemModifierId = modifierId,
                    },
                    SoldSettlementId = "town_V1",
                    BoughtTime = new CampaignTime(12345),
                },
            },
        };

        byte[] bytes = Serialize(original);
        var received = await Task.Run(() => Deserialize<NetworkUpdateTradeActionLogsForParty>(bytes));
        byte[] forwarded = Serialize(received);
        var historical = Deserialize<HistoricalMessage>(forwarded);

        Assert.Equal(bytes, forwarded);
        Assert.Equal(original.PartyId, historical.PartyId);
        var row = Assert.Single(historical.Rows);
        Assert.Equal("unregistered-caravan-item", row.Item.ItemObjectId);
        Assert.Equal(modifierId, row.Item.ItemModifierId);
        Assert.Equal(7, row.Item.Amount);
        Assert.Equal(14, row.BuyPrice);
        Assert.Equal(19, row.SellPrice);
        Assert.Equal(12345, row.BoughtTime.NumTicks);
    }

    private static byte[] Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        return stream.ToArray();
    }

    private static T Deserialize<T>(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Serializer.Deserialize<T>(stream);
    }

    /// <summary>Historical outer wire contract, without native roster reconstruction.</summary>
    [ProtoContract]
    private sealed class HistoricalMessage
    {
        [ProtoMember(1)] public string PartyId { get; set; } = null!;
        [ProtoMember(2)] public List<HistoricalRow> Rows { get; set; } = new();
    }

    /// <summary>Historical row with the roster surrogate exposed as data.</summary>
    [ProtoContract]
    private sealed class HistoricalRow
    {
        [ProtoMember(1)] public string BoughtSettlementId { get; set; } = null!;
        [ProtoMember(2)] public int BuyPrice { get; set; }
        [ProtoMember(3)] public int SellPrice { get; set; }
        [ProtoMember(4)] public ItemRosterElementSurrogate Item { get; set; }
        [ProtoMember(5)] public string SoldSettlementId { get; set; } = null!;
        [ProtoMember(6)] public CampaignTime BoughtTime { get; set; }
    }
}
