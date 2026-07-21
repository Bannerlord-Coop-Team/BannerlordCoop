using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Tests the authoritative troop reserves supplied to coop battle owners.</summary>
public class BattleTroopReserveBuilderTests : MissionTestEnvironment
{
    public BattleTroopReserveBuilderTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PreparePlan_RepeatedCallKeepsFirstBattleSize()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var troop = Server.CreateRegisteredObject<CharacterObject>("frozen_plan_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(troop, 10);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 5);
            reserveBuilder.PreparePlan(mapEvent, battleSize: 100);

            int attackerInitial = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: false)
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);
            int defenderInitial = reserveBuilder.GetOwnedReserves(mapEvent, "defender", isHost: false)
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);

            Assert.Equal(5, attackerInitial + defenderInitial);

            int duplicateInitial = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: false)
                .Concat(reserveBuilder.GetOwnedReserves(mapEvent, "defender", isHost: false))
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);
            Assert.Equal(5, duplicateInitial);

            reserveBuilder.ForgetController(mapEvent, "attacker");
            int rejoinInitial = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: false)
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);
            Assert.Equal(attackerInitial, rejoinInitial);
        });
    }

    [Fact]
    public void GrantUnassignedInitialSpawns_PersistsEntitlementAndMarksOnlyTheNewParty()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var troop = Server.CreateRegisteredObject<CharacterObject>("live_party_grant_troop");
            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(troop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 5);

            var directPlayerParty = mapEvent.AttackerSide.Parties[0];
            Assert.Equal(2, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, directPlayerParty, out var directIsPostPlan));
            Assert.False(directIsPostPlan);
            int directReserveGrant = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: false)
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);
            Assert.Equal(2, directReserveGrant);

            var aiParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            aiParty.Party.MemberRoster.AddToCounts(troop, 3);
            aiParty.Party.MapEventSide = mapEvent.AttackerSide;
            var aiMapEventParty = mapEvent.AttackerSide.Parties.Last(party => party.Party == aiParty.Party);

            Assert.Equal(1, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, aiMapEventParty, out var aiIsPostPlan));
            Assert.True(aiIsPostPlan);
            Assert.Equal(1, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, aiMapEventParty, out var repeatedIsPostPlan));
            Assert.False(repeatedIsPostPlan);

            Assert.True(Server.ObjectManager.TryGetId(aiMapEventParty, out var aiPartyId));
            var aiReserve = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: true)
                .SelectMany(reserve => reserve.Parties)
                .Single(party => party.PartyId == aiPartyId);
            Assert.Equal(1, aiReserve.InitialSpawnCount);
        });
    }

    [Fact]
    public void GrantUnassignedInitialSpawns_ZeroCapacityStillMarksNewParty()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var troop = Server.CreateRegisteredObject<CharacterObject>("zero_live_party_grant_troop");
            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(troop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 4);

            var aiParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            aiParty.Party.MemberRoster.AddToCounts(troop, 3);
            aiParty.Party.MapEventSide = mapEvent.AttackerSide;
            var aiMapEventParty = mapEvent.AttackerSide.Parties.Last(party => party.Party == aiParty.Party);

            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, aiMapEventParty, out var isPostPlanAddition));
            Assert.True(isPostPlanAddition);
        });
    }

    [Fact]
    public void GetOwnedReserves_ExcludesTroopsThatCannotJoinBattle()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.DefenderSide.Parties[0];
            var troop = Server.CreateRegisteredObject<CharacterObject>("reserve_eligibility_troop");
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            party.Party.MemberRoster.Clear();
            party.Party.MemberRoster.AddToCounts(troop, 4, woundedCount: 1);
            party.Update();

            var battleReady = party.Troops
                .Where(element => !element.IsWounded && !element.IsRouted && !element.IsKilled)
                .ToArray();
            Assert.Equal(3, battleReady.Length);

            party.OnTroopKilled(battleReady[0].Descriptor);
            party.OnTroopRouted(battleReady[1].Descriptor);

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 1000);
            var reserves = reserveBuilder.GetOwnedReserves(mapEvent, "defender", isHost: false);
            var defenderReserve = reserves.Single(reserve => reserve.Side == BattleSideEnum.Defender);
            var partyReserve = Assert.Single(defenderReserve.Parties);
            var entry = Assert.Single(partyReserve.Entries);

            Assert.Equal(troopId, entry.CharacterId);
            Assert.Equal(1, partyReserve.InitialSpawnCount);
        });
    }
}
