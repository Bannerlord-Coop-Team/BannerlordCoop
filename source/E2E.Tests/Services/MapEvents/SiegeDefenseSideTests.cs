using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Exercises canonical side selection and replicated membership in synthetic siege map events.</summary>
public class SiegeDefenseSideTests : MapEventTestBase
{
    public SiegeDefenseSideTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, BattleSideEnum.Defender, BattleSideEnum.Attacker)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleSideEnum.Attacker, BattleSideEnum.Defender)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, BattleSideEnum.Defender, BattleSideEnum.Attacker)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, BattleSideEnum.Attacker, BattleSideEnum.Defender)]
    public void ClientJoin_StaleMissionSide_UsesRequestedSlotAndReplicatesOneMembership(
        MapEvent.BattleTypes battleType, BattleSideEnum requestedSide, BattleSideEnum staleSide)
    {
        var battle = CreateSiegeMapEvent(battleType);
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        string? partyBaseId = null;

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            var mapEvent = Get<MapEvent>(client, battle.MapEventId);
            var party = Get<MobileParty>(client, partyId);
            var side = mapEvent.GetMapEventSide(requestedSide);
            Assert.True(client.ObjectManager.TryGetId(party.Party, out partyBaseId));

            // Reproduce a client whose side slot arrived before its MissionSide update.
            using (new AllowedThread()) side.MissionSide = staleSide;
            Assert.Equal(staleSide, side.MissionSide);
            Assert.Null(party.Party.MapEventSide);

            party.Party.MapEventSide = side;
            party.Party.MapEventSide = side;

            Assert.Null(party.MapEvent);
            Assert.Null(mapEvent.FindMapEventParty(party.Party));
        }, WithoutNetworkDelivery());

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Assert.Equal(battle.MapEventId, request.MapEventId);
        Assert.Equal(partyBaseId, request.PartyId);
        Assert.Equal(requestedSide, request.Side);

        Server.NetworkSentMessages.Clear();
        Server.Call(() => Server.SimulateMessage(client.NetPeer, request), MapEventDisabledMethods);

        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>()).Accepted);
        var added = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAddBattleParty>());
        string? authoritativeMembershipId = null;
        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, battle.MapEventId);
            var party = Get<MobileParty>(Server, partyId);
            var membership = mapEvent.FindMapEventParty(party.Party);
            Assert.NotNull(membership);
            Assert.True(Server.ObjectManager.TryGetId(membership, out authoritativeMembershipId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.GetMapEventSide(requestedSide), out var sideId));
            Assert.Equal(sideId, added.MapEventSideId);
        });

        Assert.NotNull(authoritativeMembershipId);
        foreach (var instance in Clients.Append(Server))
            AssertMembership(instance, battle.MapEventId, partyId, requestedSide, authoritativeMembershipId);

        client.NetworkSentMessages.Clear();
        Server.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            var mapEvent = Get<MapEvent>(client, battle.MapEventId);
            var party = Get<MobileParty>(client, partyId);
            mapEvent.GetMapEventSide(requestedSide).AddPartyInternal(party.Party);
        }, MapEventDisabledMethods);

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAddBattleParty>());
        foreach (var instance in Clients.Append(Server))
            AssertMembership(instance, battle.MapEventId, partyId, requestedSide, authoritativeMembershipId);
    }

    [Fact]
    public void ClientJoin_UnassignedSiegeSide_RejectsWithoutRequestOrMembership()
    {
        var battle = CreateSiegeMapEvent(MapEvent.BattleTypes.Siege);
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            var mapEvent = Get<MapEvent>(client, battle.MapEventId);
            var party = Get<MobileParty>(client, partyId);
            MapEventSide unassignedSide;
            using (new AllowedThread())
                unassignedSide = new MapEventSide(mapEvent, BattleSideEnum.Defender, mapEvent.DefenderSide.LeaderParty);
            Assert.Same(mapEvent, unassignedSide.MapEvent);
            Assert.Equal(BattleSideEnum.Defender, unassignedSide.MissionSide);
            Assert.NotSame(mapEvent.DefenderSide, unassignedSide);
            Assert.NotSame(mapEvent.AttackerSide, unassignedSide);

            party.Party.MapEventSide = unassignedSide;

            Assert.Null(party.Party.MapEventSide);
            Assert.Null(party.MapEvent);
        }, WithoutNetworkDelivery());

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var mapEvent = Get<MapEvent>(instance, battle.MapEventId);
                var party = Get<MobileParty>(instance, partyId);
                Assert.Null(party.Party.MapEventSide);
                Assert.Null(mapEvent.FindMapEventParty(party.Party));
            });
        }
    }

    private MapEventContext CreateSiegeMapEvent(MapEvent.BattleTypes battleType)
    {
        var battle = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        // Native siege reinforcement recalculates settlement advantage.
        Server.Call(() => Get<MapEvent>(Server, battle.MapEventId).MapEventSettlement =
            Get<Settlement>(Server, settlementId));
        // Keep native scene setup outside this test; exercise the replicated siege join graph.
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Get<MapEvent>(instance, battle.MapEventId)._mapEventType = battleType);
        return battle;
    }

    private IReadOnlyList<MethodBase> WithoutNetworkDelivery() => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(TestNetworkRouter), nameof(TestNetworkRouter.SendReliablePayload),
            new[] { typeof(NetPeer), typeof(NetPeer), typeof(byte[]) }))
        .ToList();

    private static void AssertMembership(EnvironmentInstance instance, string mapEventId,
        string partyId, BattleSideEnum expectedSide, string membershipId)
    {
        instance.Call(() =>
        {
            var mapEvent = Get<MapEvent>(instance, mapEventId);
            var party = Get<MobileParty>(instance, partyId);
            var side = mapEvent.GetMapEventSide(expectedSide);
            Assert.Same(mapEvent, party.MapEvent);
            Assert.Same(side, party.Party.MapEventSide);
            var membership = Assert.Single(mapEvent._sides.SelectMany(candidate => candidate.Parties),
                candidate => ReferenceEquals(candidate.Party, party.Party));
            Assert.Contains(membership, side.Parties);
            Assert.Same(Get<MapEventParty>(instance, membershipId), membership);
        });
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
