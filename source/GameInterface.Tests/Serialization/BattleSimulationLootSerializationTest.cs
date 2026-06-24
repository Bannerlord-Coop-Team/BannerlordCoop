using System.IO;
using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BattleSimulationLootSerializationTest
{
    [Fact]
    public void NetworkBattleSimulationLoot_RoundTrips()
    {
        var original = new NetworkBattleSimulationLoot(
            "mapEvent_1",
            BattleState.AttackerVictory,
            new[]
            {
                new BattleSimDefeatedParty(
                    "party_looters",
                    new[] { new BattleSimCasualty("looter", isHero: false, number: 3, woundedNumber: 0) },
                    new[] { new BattleSimCasualty("looter", isHero: false, number: 1, woundedNumber: 1) }),
            },
            new[] { new BattleSimWinner("party_player", 42) });

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkBattleSimulationLoot>(stream);

        Assert.Equal(original.MapEventId, copy.MapEventId);
        Assert.Equal(original.WinningState, copy.WinningState);
        Assert.Single(copy.DefeatedParties);

        var party = copy.DefeatedParties[0];
        Assert.Equal("party_looters", party.PartyId);

        Assert.Single(party.Died);
        Assert.Equal("looter", party.Died[0].CharacterId);
        Assert.Equal(3, party.Died[0].Number);

        Assert.Single(party.Wounded);
        Assert.Equal(1, party.Wounded[0].Number);
        Assert.Equal(1, party.Wounded[0].WoundedNumber);

        Assert.Single(copy.Winners);
        Assert.Equal("party_player", copy.Winners[0].PartyId);
        Assert.Equal(42, copy.Winners[0].ContributionToBattle);
    }
}
