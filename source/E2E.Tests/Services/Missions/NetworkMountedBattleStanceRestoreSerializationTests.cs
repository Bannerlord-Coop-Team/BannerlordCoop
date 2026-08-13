#if DEBUG
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Services.Stances.Handlers;
using GameInterface.Services.Stances.Messages;
using ProtoBuf;
using System.IO;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class NetworkMountedBattleStanceRestoreSerializationTests
{
    [Fact]
    public void NetworkRestoreMountedBattleStance_RoundTripsCompleteSnapshot()
    {
        var original = new NetworkRestoreMountedBattleStance(
            "fixture-2983",
            "Faction_1",
            "Faction_2",
            stanceType: 1,
            behaviorPriority: 2,
            warStartDateTicks: 3,
            peaceDeclarationDateTicks: 4,
            troopCasualties1: 5,
            troopCasualties2: 6,
            shipCasualties1: 7,
            shipCasualties2: 8,
            successfulSieges1: 9,
            successfulSieges2: 10,
            successfulRaids1: 11,
            successfulRaids2: 12,
            totalTributePaidFrom1To2: 13,
            dailyTributeFrom1To2: 14,
            dailyTributeInstallments: 15,
            successfulTownSieges1: 16,
            successfulTownSieges2: 17,
            hasFaction1PoliticalStagnation: true,
            faction1PoliticalStagnation: 18,
            hasFaction2PoliticalStagnation: false,
            faction2PoliticalStagnation: 0,
            stanceLinkWasAbsent: true,
            faction1WasAtWarWithFaction2: true,
            faction2WasAtWarWithFaction1: true);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkRestoreMountedBattleStance>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.FixtureToken, result.FixtureToken);
        Assert.Equal(original.Faction1Id, result.Faction1Id);
        Assert.Equal(original.Faction2Id, result.Faction2Id);
        Assert.Equal(original.StanceType, result.StanceType);
        Assert.Equal(original.BehaviorPriority, result.BehaviorPriority);
        Assert.Equal(original.WarStartDateTicks, result.WarStartDateTicks);
        Assert.Equal(
            original.PeaceDeclarationDateTicks,
            result.PeaceDeclarationDateTicks);
        Assert.Equal(original.TroopCasualties1, result.TroopCasualties1);
        Assert.Equal(original.TroopCasualties2, result.TroopCasualties2);
        Assert.Equal(original.ShipCasualties1, result.ShipCasualties1);
        Assert.Equal(original.ShipCasualties2, result.ShipCasualties2);
        Assert.Equal(original.SuccessfulSieges1, result.SuccessfulSieges1);
        Assert.Equal(original.SuccessfulSieges2, result.SuccessfulSieges2);
        Assert.Equal(original.SuccessfulRaids1, result.SuccessfulRaids1);
        Assert.Equal(original.SuccessfulRaids2, result.SuccessfulRaids2);
        Assert.Equal(
            original.TotalTributePaidFrom1To2,
            result.TotalTributePaidFrom1To2);
        Assert.Equal(
            original.DailyTributeFrom1To2,
            result.DailyTributeFrom1To2);
        Assert.Equal(
            original.DailyTributeInstallments,
            result.DailyTributeInstallments);
        Assert.Equal(
            original.SuccessfulTownSieges1,
            result.SuccessfulTownSieges1);
        Assert.Equal(
            original.SuccessfulTownSieges2,
            result.SuccessfulTownSieges2);
        Assert.True(result.HasFaction1PoliticalStagnation);
        Assert.Equal(18, result.Faction1PoliticalStagnation);
        Assert.False(result.HasFaction2PoliticalStagnation);
        Assert.True(result.RestoreExactSnapshot);
        Assert.True(result.StanceLinkWasAbsent);
        Assert.True(result.Faction1WasAtWarWithFaction2);
        Assert.True(result.Faction2WasAtWarWithFaction1);
    }

    [Fact]
    public void NetworkRestoreMountedBattleStance_LegacyPayloadDefaultsToExistingStanceLink()
    {
        var typeMapper = new SerializableTypeMapper();
        Assert.True(typeMapper.TryGetId(
            typeof(NetworkRestoreMountedBattleStance),
            out int typeId));

        byte[] payload;
        using (var payloadStream = new MemoryStream())
        {
            Serializer.Serialize(
                payloadStream,
                new LegacyMountedBattleStanceRestorePayload
                {
                    FixtureToken = "fixture-2983",
                    RestoreExactSnapshot = true
                });
            payload = payloadStream.ToArray();
        }

        byte[] wire;
        using (var wrapperStream = new MemoryStream())
        {
            Serializer.Serialize(
                wrapperStream,
                new LegacyMessageWrapper
                {
                    TypeId = typeId,
                    Data = payload
                });
            wire = wrapperStream.ToArray();
        }

        var serializer = new ProtoBufSerializer(typeMapper);
        var result = Assert.IsType<NetworkRestoreMountedBattleStance>(
            serializer.Deserialize<IMessage>(wire));

        Assert.Equal("fixture-2983", result.FixtureToken);
        Assert.True(result.RestoreExactSnapshot);
        Assert.False(result.StanceLinkWasAbsent);
        Assert.False(result.Faction1WasAtWarWithFaction2);
        Assert.False(result.Faction2WasAtWarWithFaction1);
    }

    [Fact]
    public void NetworkRestoreMountedBattleStance_RoundTripsFixtureWarMode()
    {
        var original = new NetworkRestoreMountedBattleStance(
            "fixture-2983",
            "Faction_1",
            "Faction_2",
            stanceType: (int)StanceType.War,
            behaviorPriority: 0,
            warStartDateTicks: 0,
            peaceDeclarationDateTicks: 0,
            troopCasualties1: 0,
            troopCasualties2: 0,
            shipCasualties1: 0,
            shipCasualties2: 0,
            successfulSieges1: 0,
            successfulSieges2: 0,
            successfulRaids1: 0,
            successfulRaids2: 0,
            totalTributePaidFrom1To2: 0,
            dailyTributeFrom1To2: 0,
            dailyTributeInstallments: 0,
            successfulTownSieges1: 0,
            successfulTownSieges2: 0,
            hasFaction1PoliticalStagnation: false,
            faction1PoliticalStagnation: 0,
            hasFaction2PoliticalStagnation: false,
            faction2PoliticalStagnation: 0,
            restoreExactSnapshot: false);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkRestoreMountedBattleStance>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.False(result.RestoreExactSnapshot);
        Assert.Equal((int)StanceType.War, result.StanceType);
    }

    [Fact]
    public void ApplyMountedBattleStanceFields_RemapReversedClientOrientation()
    {
        var faction1 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var faction2 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        faction1.StringId = "Faction_1";
        faction2.StringId = "Faction_2";
        var stance = new StanceLink(StanceType.Neutral, faction2, faction1);
        var restore = new NetworkRestoreMountedBattleStance(
            "fixture-2983",
            faction1.StringId,
            faction2.StringId,
            stanceType: (int)StanceType.War,
            behaviorPriority: 2,
            warStartDateTicks: 3,
            peaceDeclarationDateTicks: 4,
            troopCasualties1: 5,
            troopCasualties2: 6,
            shipCasualties1: 7,
            shipCasualties2: 8,
            successfulSieges1: 9,
            successfulSieges2: 10,
            successfulRaids1: 11,
            successfulRaids2: 12,
            totalTributePaidFrom1To2: 13,
            dailyTributeFrom1To2: 14,
            dailyTributeInstallments: 15,
            successfulTownSieges1: 16,
            successfulTownSieges2: 17,
            hasFaction1PoliticalStagnation: false,
            faction1PoliticalStagnation: 0,
            hasFaction2PoliticalStagnation: false,
            faction2PoliticalStagnation: 0);

        FactionStanceHandler.ApplyMountedBattleStanceFields(
            restore,
            stance,
            faction1,
            faction2);

        Assert.Equal(StanceType.War, stance._stanceType);
        Assert.Equal(6, stance._troopCasualties1);
        Assert.Equal(5, stance._troopCasualties2);
        Assert.Equal(8, stance.ShipCasualties1);
        Assert.Equal(7, stance.ShipCasualties2);
        Assert.Equal(10, stance._successfulSieges1);
        Assert.Equal(9, stance._successfulSieges2);
        Assert.Equal(12, stance._successfulRaids1);
        Assert.Equal(11, stance._successfulRaids2);
        Assert.Equal(-13, stance._totalTributePaidFrom1To2);
        Assert.Equal(-14, stance._dailyTributeFrom1To2);
        Assert.Equal(15, stance._dailyTributeInstallments);
        Assert.Equal(17, stance._successfulTownSieges1);
        Assert.Equal(16, stance._successfulTownSieges2);
    }

    [ProtoContract]
    private sealed class LegacyMountedBattleStanceRestorePayload
    {
        [ProtoMember(1)] public string FixtureToken { get; set; }
        [ProtoMember(25)] public bool RestoreExactSnapshot { get; set; }
    }

    [ProtoContract]
    private sealed class LegacyMessageWrapper
    {
        [ProtoMember(1)] public int TypeId { get; set; }
        [ProtoMember(2)] public byte[] Data { get; set; }
    }
}
#endif
