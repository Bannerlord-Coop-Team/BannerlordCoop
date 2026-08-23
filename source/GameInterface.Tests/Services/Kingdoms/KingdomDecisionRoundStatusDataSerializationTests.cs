using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System;
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
            },
            12000);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        KingdomDecisionRoundStatusData copy = Serializer.Deserialize<KingdomDecisionRoundStatusData>(stream);

        Assert.Equal(original.KingdomId, copy.KingdomId);
        Assert.Equal(original.DecisionIndex, copy.DecisionIndex);
        Assert.Equal(original.DeadlineUtcTicks, copy.DeadlineUtcTicks);
        Assert.Equal(original.ServerUtcTicks, copy.ServerUtcTicks);
        Assert.Equal(original.OrderedOutcomeKeys, copy.OrderedOutcomeKeys);
        Assert.Equal(original.Clans[0].ClanId, copy.Clans[0].ClanId);
    }

    [Fact]
    public void GetLocalDeadlineUtc_UsesServerRelativeDurationAcrossClockOffset()
    {
        var serverUtcNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var clientUtcNow = serverUtcNow - TimeSpan.FromHours(8);
        var status = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            0,
            (serverUtcNow + TimeSpan.FromSeconds(45)).Ticks,
            Array.Empty<KingdomDecisionRoundClanStatusData>(),
            serverUtcTicks: serverUtcNow.Ticks);

        DateTime localDeadline = status.GetLocalDeadlineUtc(clientUtcNow);

        Assert.Equal(clientUtcNow + TimeSpan.FromSeconds(45), localDeadline);
    }

    [Fact]
    public void GetLocalDeadlineUtc_UsesLegacyAbsoluteDeadlineWithoutServerTime()
    {
        var clientUtcNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var status = new KingdomDecisionRoundStatusData(
            "kingdom_s",
            0,
            (clientUtcNow + TimeSpan.FromSeconds(30)).Ticks,
            Array.Empty<KingdomDecisionRoundClanStatusData>());

        DateTime localDeadline = status.GetLocalDeadlineUtc(clientUtcNow);

        Assert.Equal(clientUtcNow + TimeSpan.FromSeconds(30), localDeadline);
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
