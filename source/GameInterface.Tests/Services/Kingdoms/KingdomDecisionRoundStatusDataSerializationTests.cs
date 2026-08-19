using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomDecisionRoundStatusDataSerializationTests
{
    [Fact]
    public void Serialize_ThenDeserialize_PreservesOrderedOutcomeKeys()
    {
        var original = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            2,
            12345,
            new[]
            {
                new KingdomDecisionRoundClanStatusData("clan_a", "Clan A", "Alice", true, true),
            },
            new[]
            {
                "TaleWorlds.CampaignSystem.Election.SettlementClaimantDecision+ClanAsDecisionOutcome:Clan=clan_fen_beannis",
                "TaleWorlds.CampaignSystem.Election.SettlementClaimantDecision+ClanAsDecisionOutcome:Clan=clan_pethros",
            });

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        KingdomDecisionRoundStatusData copy = Serializer.Deserialize<KingdomDecisionRoundStatusData>(stream);

        Assert.Equal(original.KingdomId, copy.KingdomId);
        Assert.Equal(original.DecisionIndex, copy.DecisionIndex);
        Assert.Equal(original.DeadlineUtcTicks, copy.DeadlineUtcTicks);
        Assert.Equal(original.OrderedOutcomeKeys, copy.OrderedOutcomeKeys);
        Assert.Equal(original.Clans[0].ClanId, copy.Clans[0].ClanId);
    }

    [Fact]
    public void Deserialize_WithoutOrderedOutcomeKeys_UsesEmptyArray()
    {
        var original = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            0,
            1,
            System.Array.Empty<KingdomDecisionRoundClanStatusData>());

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        KingdomDecisionRoundStatusData copy = Serializer.Deserialize<KingdomDecisionRoundStatusData>(stream);

        Assert.True(copy.OrderedOutcomeKeys == null || copy.OrderedOutcomeKeys.Length == 0);
    }

    [Fact]
    public void HasSameContent_IgnoresInstanceIdentity()
    {
        var left = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            1,
            99,
            new[]
            {
                new KingdomDecisionRoundClanStatusData("clan_a", "Clan A", "Alice", false, true),
            },
            new[] { "outcome-a" });
        var right = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            1,
            99,
            new[]
            {
                new KingdomDecisionRoundClanStatusData("clan_a", "Clan A", "Alice", false, true),
            },
            new[] { "outcome-a" });
        var changed = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            1,
            99,
            new[]
            {
                new KingdomDecisionRoundClanStatusData("clan_a", "Clan A", "Alice", true, true),
            },
            new[] { "outcome-a" });

        Assert.True(left.HasSameContent(right));
        Assert.False(left.HasSameContent(changed));
    }
}
