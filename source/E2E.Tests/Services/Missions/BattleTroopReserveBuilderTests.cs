using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
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
                mapEvent, directPlayerParty, out var directIsPostPlan, out var directIsWaiting));
            Assert.False(directIsPostPlan);
            Assert.False(directIsWaiting);
            int directReserveGrant = reserveBuilder.GetOwnedReserves(mapEvent, "attacker", isHost: false)
                .SelectMany(reserve => reserve.Parties)
                .Sum(party => party.InitialSpawnCount);
            Assert.Equal(2, directReserveGrant);

            var aiParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            aiParty.Party.MemberRoster.AddToCounts(troop, 3);
            aiParty.Party.MapEventSide = mapEvent.AttackerSide;
            var aiMapEventParty = mapEvent.AttackerSide.Parties.Last(party => party.Party == aiParty.Party);

            Assert.Equal(1, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, aiMapEventParty, out var aiIsPostPlan, out var aiIsWaiting));
            Assert.True(aiIsPostPlan);
            Assert.False(aiIsWaiting);
            Assert.Equal(1, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, aiMapEventParty, out var repeatedIsPostPlan, out var repeatedIsWaiting));
            Assert.False(repeatedIsPostPlan);
            Assert.False(repeatedIsWaiting);

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
                mapEvent, aiMapEventParty, out var isPostPlanAddition, out var isWaiting));
            Assert.True(isPostPlanAddition);
            Assert.False(isWaiting);
        });
    }

    [Fact]
    public void GrantUnassignedInitialSpawns_FullPlanQueuesDirectPlayerAndPlacesHeroFirst()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var latePartyId = CreateLatePlayerParty("late-player", out var playerCharacterId);
        var fillerTroopId = CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(latePartyId, out var lateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("full_plan_initial_troop");
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fillerTroopId, out var fillerTroop));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(
                playerCharacterId, out var playerCharacter));

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);

            lateParty.Party.MemberRoster.Clear();
            lateParty.Party.MemberRoster.AddToCounts(fillerTroop, 1);
            lateParty.Party.MemberRoster.AddToCounts(playerCharacter, 1);
            lateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var lateMapEventParty = mapEvent.AttackerSide.Parties.Single(party => party.Party == lateParty.Party);

            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, lateMapEventParty, out var isPostPlanAddition, out var waitsForPrioritySlot));
            Assert.False(isPostPlanAddition);
            Assert.True(waitsForPrioritySlot);

            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, lateMapEventParty, out var repeatedIsPostPlan, out var repeatedWaits));
            Assert.False(repeatedIsPostPlan);
            Assert.True(repeatedWaits);

            Assert.True(Server.ObjectManager.TryGetId(lateMapEventParty, out var lateMapEventPartyId));
            var reserve = reserveBuilder.GetOwnedReserves(mapEvent, "late-player", isHost: false)
                .SelectMany(side => side.Parties)
                .Single(party => party.PartyId == lateMapEventPartyId);
            Assert.Equal(0, reserve.InitialSpawnCount);
            Assert.Equal(playerCharacterId, reserve.Entries[0].CharacterId);
        });
    }

    [Fact]
    public void TryTransferInitialSpawnOnDeparture_TransfersInFifoOrderAndSettlesConsumedOwnership()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var firstLatePartyId = CreateLatePlayerParty("late-first");
        var secondLatePartyId = CreateLatePlayerParty("late-second");
        var thirdLatePartyId = CreateLatePlayerParty("late-third");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(firstLatePartyId, out var firstLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(secondLatePartyId, out var secondLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(thirdLatePartyId, out var thirdLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("fifo_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("fifo_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);

            firstLateParty.Party.MemberRoster.AddToCounts(lateTroop, 1);
            firstLateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var firstLateMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == firstLateParty.Party);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, firstLateMapEventParty, out _, out var firstWaits));
            Assert.True(firstWaits);

            secondLateParty.Party.MemberRoster.AddToCounts(lateTroop, 1);
            secondLateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var secondLateMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == secondLateParty.Party);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, secondLateMapEventParty, out _, out var secondWaits));
            Assert.True(secondWaits);

            thirdLateParty.Party.MemberRoster.AddToCounts(lateTroop, 1);
            thirdLateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var thirdLateMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == thirdLateParty.Party);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, thirdLateMapEventParty, out _, out var thirdWaits));
            Assert.True(thirdWaits);

            Assert.True(Server.ObjectManager.TryGetId(firstLateMapEventParty, out var firstLateMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(secondLateMapEventParty, out var secondLateMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(thirdLateMapEventParty, out var thirdLateMapEventPartyId));
            var attackerDonor = mapEvent.AttackerSide.Parties.First(party => party != firstLateMapEventParty
                && party != secondLateMapEventParty);
            var defenderDonor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(attackerDonor, out var attackerDonorId));
            Assert.True(Server.ObjectManager.TryGetId(defenderDonor, out var defenderDonorId));

            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, attackerDonorId, out var firstTransfer));
            Assert.Equal(firstLateMapEventPartyId, firstTransfer.WaitingPartyId);
            Assert.Equal(attackerDonorId, firstTransfer.DonorPartyId);

            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, defenderDonorId, out var secondTransfer));
            Assert.Equal(secondLateMapEventPartyId, secondTransfer.WaitingPartyId);
            Assert.Equal(defenderDonorId, secondTransfer.DonorPartyId);
            Assert.NotEqual(firstTransfer.TransferId, secondTransfer.TransferId);

            Assert.True(reserveBuilder.CompletePrioritySpawn(
                mapEvent, firstTransfer.TransferId, firstLateMapEventPartyId));
            Assert.True(reserveBuilder.CompletePrioritySpawn(
                mapEvent, secondTransfer.TransferId, secondLateMapEventPartyId));

            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent,
                firstLateMapEventPartyId,
                out var thirdTransfer,
                out var firstSettledTransfer));
            Assert.Equal(firstTransfer.TransferId, firstSettledTransfer.TransferId);
            Assert.Equal(firstLateMapEventPartyId, firstSettledTransfer.WaitingPartyId);
            Assert.Equal(thirdLateMapEventPartyId, thirdTransfer.WaitingPartyId);
            Assert.Equal(firstLateMapEventPartyId, thirdTransfer.DonorPartyId);

            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent,
                secondLateMapEventPartyId,
                out var noTransfer,
                out var secondSettledTransfer));
            Assert.Equal(0, noTransfer.TransferId);
            Assert.Equal(secondTransfer.TransferId, secondSettledTransfer.TransferId);
            Assert.Equal(secondLateMapEventPartyId, secondSettledTransfer.WaitingPartyId);

            var replayable = Assert.Single(reserveBuilder.GetPrioritySlotSnapshot(mapEvent));
            Assert.Equal(thirdTransfer.TransferId, replayable.TransferId);
            Assert.Equal(thirdLateMapEventPartyId, replayable.WaitingPartyId);

            Assert.Equal(0, GetInitialSpawnCount(reserveBuilder, mapEvent, "attacker", attackerDonorId));
            Assert.Equal(0, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", defenderDonorId));
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-first", firstLateMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-second", secondLateMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-third", thirdLateMapEventPartyId));
            Assert.Equal(2,
                GetInitialSpawnCount(reserveBuilder, mapEvent, "attacker", attackerDonorId)
                + GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", defenderDonorId)
                + GetInitialSpawnCount(reserveBuilder, mapEvent, "late-first", firstLateMapEventPartyId)
                + GetInitialSpawnCount(reserveBuilder, mapEvent, "late-second", secondLateMapEventPartyId)
                + GetInitialSpawnCount(reserveBuilder, mapEvent, "late-third", thirdLateMapEventPartyId));

            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlot(
                mapEvent,
                secondLateMapEventPartyId,
                out var releasedTransfer,
                out var released));
            Assert.True(released);
            Assert.Equal(secondTransfer.TransferId, releasedTransfer.TransferId);
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "defender", defenderDonorId));
        });
    }

    [Fact]
    public void TryTransferRetainedInitialSpawn_CasualtyWithoutWaiterLetsLaterPlayerClaimVacancyInFifoOrder()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var firstLatePartyId = CreateLatePlayerParty("retained-first");
        var secondLatePartyId = CreateLatePlayerParty("retained-second");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(firstLatePartyId, out var firstLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(secondLatePartyId, out var secondLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("retained_casualty_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("retained_casualty_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 1);
                party.Update();
            }

            var reserveBuilder = CreateIsolatedReserveBuilder();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));

            Assert.False(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var immediateTransfer));
            Assert.Equal(0, immediateTransfer.TransferId);

            var firstLateMapEventParty = AddLateParty(mapEvent.AttackerSide, firstLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, firstLateMapEventParty, out _, out var firstWaits));
            Assert.True(firstWaits);

            var secondLateMapEventParty = AddLateParty(mapEvent.AttackerSide, secondLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, secondLateMapEventParty, out _, out var secondWaits));
            Assert.True(secondWaits);

            Assert.True(Server.ObjectManager.TryGetId(firstLateMapEventParty, out var firstLateMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(secondLateMapEventParty, out var secondLateMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferRetainedInitialSpawn(
                mapEvent, out var transfer, out var settledTransfer));
            Assert.Equal(0, settledTransfer.TransferId);
            Assert.Equal(donorId, transfer.DonorPartyId);
            Assert.Equal(firstLateMapEventPartyId, transfer.WaitingPartyId);
            Assert.False(reserveBuilder.TryTransferRetainedInitialSpawn(mapEvent, out _, out _));

            Assert.Equal(0, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-first", firstLateMapEventPartyId));
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-second", secondLateMapEventPartyId));
        });
    }

    [Fact]
    public void RetainInitialSpawnVacancies_RepeatedCallKeepsOneWholePartyVacancySet()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var firstLatePartyId = CreateLatePlayerParty("retained-whole-first");
        var secondLatePartyId = CreateLatePlayerParty("retained-whole-second");
        var thirdLatePartyId = CreateLatePlayerParty("retained-whole-third");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(firstLatePartyId, out var firstLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(secondLatePartyId, out var secondLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(thirdLatePartyId, out var thirdLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("retained_whole_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("retained_whole_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = CreateIsolatedReserveBuilder();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 4);
            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.Equal(2, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));
            Assert.Equal(2, reserveBuilder.RetainInitialSpawnVacancies(mapEvent, donorId));
            Assert.Equal(2, reserveBuilder.RetainInitialSpawnVacancies(mapEvent, donorId));

            var firstLateMapEventParty = AddLateParty(mapEvent.AttackerSide, firstLateParty, lateTroop);
            var secondLateMapEventParty = AddLateParty(mapEvent.AttackerSide, secondLateParty, lateTroop);
            var thirdLateMapEventParty = AddLateParty(mapEvent.AttackerSide, thirdLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, firstLateMapEventParty, out _, out var firstWaits));
            Assert.True(firstWaits);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, secondLateMapEventParty, out _, out var secondWaits));
            Assert.True(secondWaits);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, thirdLateMapEventParty, out _, out var thirdWaits));
            Assert.True(thirdWaits);

            Assert.True(Server.ObjectManager.TryGetId(firstLateMapEventParty, out var firstLateMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(secondLateMapEventParty, out var secondLateMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(thirdLateMapEventParty, out var thirdLateMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferRetainedInitialSpawn(
                mapEvent, out var firstTransfer, out var firstSettledTransfer));
            Assert.Equal(0, firstSettledTransfer.TransferId);
            Assert.Equal(donorId, firstTransfer.DonorPartyId);
            Assert.Equal(firstLateMapEventPartyId, firstTransfer.WaitingPartyId);
            Assert.True(reserveBuilder.TryTransferRetainedInitialSpawn(
                mapEvent, out var secondTransfer, out var secondSettledTransfer));
            Assert.Equal(0, secondSettledTransfer.TransferId);
            Assert.Equal(donorId, secondTransfer.DonorPartyId);
            Assert.Equal(secondLateMapEventPartyId, secondTransfer.WaitingPartyId);
            Assert.False(reserveBuilder.TryTransferRetainedInitialSpawn(mapEvent, out _, out _));

            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-whole-first", firstLateMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-whole-second", secondLateMapEventPartyId));
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-whole-third", thirdLateMapEventPartyId));
        });
    }

    [Fact]
    public void TryReassignOrReleasePrioritySlot_ReleasedAssignmentRemainsClaimable()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var departedLatePartyId = CreateLatePlayerParty("retained-released");
        var nextLatePartyId = CreateLatePlayerParty("retained-after-release");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(departedLatePartyId, out var departedLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(nextLatePartyId, out var nextLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("retained_release_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("retained_release_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 1);
                party.Update();
            }

            var reserveBuilder = CreateIsolatedReserveBuilder();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var departedMapEventParty = AddLateParty(mapEvent.AttackerSide, departedLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, departedMapEventParty, out _, out var departedWaits));
            Assert.True(departedWaits);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.True(Server.ObjectManager.TryGetId(departedMapEventParty, out var departedMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var originalTransfer));
            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlot(
                mapEvent, departedMapEventPartyId, out var releasedTransfer, out var released));
            Assert.True(released);
            Assert.Equal(originalTransfer.TransferId, releasedTransfer.TransferId);

            var nextMapEventParty = AddLateParty(mapEvent.AttackerSide, nextLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, nextMapEventParty, out _, out var nextWaits));
            Assert.True(nextWaits);
            Assert.True(Server.ObjectManager.TryGetId(nextMapEventParty, out var nextMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferRetainedInitialSpawn(
                mapEvent, out var replacementTransfer, out var settledTransfer));
            Assert.Equal(0, settledTransfer.TransferId);
            Assert.NotEqual(originalTransfer.TransferId, replacementTransfer.TransferId);
            Assert.Equal(donorId, replacementTransfer.DonorPartyId);
            Assert.Equal(nextMapEventPartyId, replacementTransfer.WaitingPartyId);

            Assert.Equal(0, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-released", departedMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "retained-after-release", nextMapEventPartyId));
        });
    }

    [Fact]
    public void TryTransferInitialSpawnOnDeparture_SkipsForgottenWaitingPlayer()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var staleLatePartyId = CreateLatePlayerParty("late-stale");
        var liveLatePartyId = CreateLatePlayerParty("late-live");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(staleLatePartyId, out var staleLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(liveLatePartyId, out var liveLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("stale_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("stale_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);

            staleLateParty.Party.MemberRoster.AddToCounts(lateTroop, 1);
            staleLateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var staleMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == staleLateParty.Party);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, staleMapEventParty, out _, out var staleWaits));
            Assert.True(staleWaits);

            liveLateParty.Party.MemberRoster.AddToCounts(lateTroop, 1);
            liveLateParty.Party.MapEventSide = mapEvent.AttackerSide;
            var liveMapEventParty = mapEvent.AttackerSide.Parties.Single(
                party => party.Party == liveLateParty.Party);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, liveMapEventParty, out _, out var liveWaits));
            Assert.True(liveWaits);

            reserveBuilder.ForgetController(mapEvent, "late-stale");

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.True(Server.ObjectManager.TryGetId(liveMapEventParty, out var liveMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var transfer));
            Assert.Equal(liveMapEventPartyId, transfer.WaitingPartyId);
        });
    }

    [Fact]
    public void TryReassignOrReleasePrioritySlot_ReusesTransferAndReleasesConsumedSlotOnDeparture()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var departedLatePartyId = CreateLatePlayerParty("late-departed");
        var nextLatePartyId = CreateLatePlayerParty("late-next");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(departedLatePartyId, out var departedLateParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(nextLatePartyId, out var nextLateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("reassign_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("reassign_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var departedMapEventParty = AddLateParty(mapEvent.AttackerSide, departedLateParty, lateTroop);
            var nextMapEventParty = AddLateParty(mapEvent.AttackerSide, nextLateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, departedMapEventParty, out _, out var departedWaits));
            Assert.True(departedWaits);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, nextMapEventParty, out _, out var nextWaits));
            Assert.True(nextWaits);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.True(Server.ObjectManager.TryGetId(departedMapEventParty, out var departedMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(nextMapEventParty, out var nextMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var originalTransfer));
            Assert.Equal(departedMapEventPartyId, originalTransfer.WaitingPartyId);

            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlot(
                mapEvent, departedMapEventPartyId, out var reassignedTransfer, out var released));
            Assert.False(released);
            Assert.Equal(originalTransfer.TransferId, reassignedTransfer.TransferId);
            Assert.Equal(nextMapEventPartyId, reassignedTransfer.WaitingPartyId);
            Assert.Equal(donorId, reassignedTransfer.DonorPartyId);
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-departed", departedMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-next", nextMapEventPartyId));
            Assert.Equal(0, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));

            Assert.False(reserveBuilder.CompletePrioritySpawn(
                mapEvent, reassignedTransfer.TransferId + 1, nextMapEventPartyId));
            Assert.False(reserveBuilder.CompletePrioritySpawn(
                mapEvent, reassignedTransfer.TransferId, departedMapEventPartyId));
            Assert.True(reserveBuilder.CompletePrioritySpawn(
                mapEvent, reassignedTransfer.TransferId, nextMapEventPartyId));
            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlot(
                mapEvent, nextMapEventPartyId, out var consumedRelease, out var consumedReleased));
            Assert.True(consumedReleased);
            Assert.Equal(reassignedTransfer.TransferId, consumedRelease.TransferId);
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-next", nextMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));
        });
    }

    [Fact]
    public void TryReassignOrReleasePrioritySlot_RestoresDonorWhenNoWaiterRemains()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var latePartyId = CreateLatePlayerParty("late-departed");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(latePartyId, out var lateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("release_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("release_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var lateMapEventParty = AddLateParty(mapEvent.AttackerSide, lateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, lateMapEventParty, out _, out var waits));
            Assert.True(waits);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.True(Server.ObjectManager.TryGetId(lateMapEventParty, out var lateMapEventPartyId));
            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var originalTransfer));

            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlot(
                mapEvent, lateMapEventPartyId, out var releasedTransfer, out var released));
            Assert.True(released);
            Assert.Equal(originalTransfer.TransferId, releasedTransfer.TransferId);
            Assert.Equal(lateMapEventPartyId, releasedTransfer.WaitingPartyId);
            Assert.Equal(donorId, releasedTransfer.DonorPartyId);
            Assert.Equal(0, GetInitialSpawnCount(
                reserveBuilder, mapEvent, "late-departed", lateMapEventPartyId));
            Assert.Equal(1, GetInitialSpawnCount(reserveBuilder, mapEvent, "defender", donorId));
            Assert.False(reserveBuilder.CompletePrioritySpawn(
                mapEvent, releasedTransfer.TransferId, lateMapEventPartyId));
        });
    }

    [Fact]
    public void TryReassignOrReleasePrioritySlot_CancelsQueuedPlayerWithoutDonorTransfer()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var latePartyId = CreateLatePlayerParty("late-queued");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(latePartyId, out var lateParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("cancel_queue_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("cancel_queue_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var lateMapEventParty = AddLateParty(mapEvent.AttackerSide, lateParty, lateTroop);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, lateMapEventParty, out _, out var waits));
            Assert.True(waits);
            Assert.True(Server.ObjectManager.TryGetId(lateMapEventParty, out var lateMapEventPartyId));

            reserveBuilder.ForgetController(mapEvent, "late-queued");
            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlotForController(
                mapEvent, "late-queued", out var cancellation, out var released));
            Assert.True(released);
            Assert.Equal(0, cancellation.TransferId);
            Assert.Equal(lateMapEventPartyId, cancellation.WaitingPartyId);
            Assert.Null(cancellation.DonorPartyId);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.False(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out _));
        });
    }

    [Fact]
    public void CancelledWaiter_RequeueAppendsAfterContinuouslyWaitingPlayer()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var rejoiningPartyId = CreateLatePlayerParty("late-rejoining");
        var continuousPartyId = CreateLatePlayerParty("late-continuous");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rejoiningPartyId, out var rejoiningParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(continuousPartyId, out var continuousParty));
            var initialTroop = Server.CreateRegisteredObject<CharacterObject>("requeue_fifo_initial_troop");
            var lateTroop = Server.CreateRegisteredObject<CharacterObject>("requeue_fifo_late_troop");

            foreach (var party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                party.Party.MemberRoster.Clear();
                party.Party.MemberRoster.AddToCounts(initialTroop, 2);
                party.Update();
            }

            var reserveBuilder = Server.Resolve<IBattleTroopReserveBuilder>();
            reserveBuilder.PreparePlan(mapEvent, battleSize: 2);
            var rejoiningMapEventParty = AddLateParty(mapEvent.AttackerSide, rejoiningParty, lateTroop);
            var continuousMapEventParty = AddLateParty(mapEvent.AttackerSide, continuousParty, lateTroop);

            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, rejoiningMapEventParty, out _, out var rejoiningWaits));
            Assert.True(rejoiningWaits);
            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, continuousMapEventParty, out _, out var continuousWaits));
            Assert.True(continuousWaits);

            Assert.True(Server.ObjectManager.TryGetId(rejoiningMapEventParty, out var rejoiningMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(continuousMapEventParty, out var continuousMapEventPartyId));
            reserveBuilder.ForgetController(mapEvent, "late-rejoining");
            Assert.True(reserveBuilder.TryReassignOrReleasePrioritySlotForController(
                mapEvent, "late-rejoining", out var cancellation, out var released));
            Assert.True(released);
            Assert.Equal(rejoiningMapEventPartyId, cancellation.WaitingPartyId);

            Assert.Equal(0, reserveBuilder.GrantUnassignedInitialSpawns(
                mapEvent, rejoiningMapEventParty, out _, out var requeuedWaits));
            Assert.True(requeuedWaits);

            var donor = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(donor, out var donorId));
            Assert.True(reserveBuilder.TryTransferInitialSpawnOnDeparture(
                mapEvent, donorId, out var transfer));
            Assert.Equal(continuousMapEventPartyId, transfer.WaitingPartyId);
            Assert.NotEqual(rejoiningMapEventPartyId, transfer.WaitingPartyId);
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

    private string CreateLatePlayerParty(string controllerId)
    {
        return CreateLatePlayerParty(controllerId, out _);
    }

    private string CreateLatePlayerParty(string controllerId, out string characterObjectId)
    {
        var heroId = CreateRegisteredObject<Hero>();
        var partyId = CreateRegisteredObject<MobileParty>();
        var registeredCharacterObjectId = CreateRegisteredObject<CharacterObject>();
        characterObjectId = registeredCharacterObjectId;

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                controllerId, heroId, partyId, "MyClanId", registeredCharacterObjectId)));
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var playerManager = client.Resolve<IPlayerManager>();
                Assert.True(playerManager.AddPlayer(new Player(
                    controllerId, heroId, partyId, "MyClanId", registeredCharacterObjectId)));
            });
        }

        return partyId;
    }

    private static MapEventParty AddLateParty(
        MapEventSide side,
        MobileParty mobileParty,
        CharacterObject troop)
    {
        mobileParty.Party.MemberRoster.AddToCounts(troop, 1);
        mobileParty.Party.MapEventSide = side;
        return side.Parties.Single(party => party.Party == mobileParty.Party);
    }

    private BattleTroopReserveBuilder CreateIsolatedReserveBuilder()
    {
        return new BattleTroopReserveBuilder(
            new BattleTroopLedger(),
            Server.ObjectManager,
            Server.Resolve<IPlayerManager>(),
            Server.Resolve<IBattleInitialSpawnAllocator>());
    }

    private static int GetInitialSpawnCount(
        IBattleTroopReserveBuilder reserveBuilder,
        MapEvent mapEvent,
        string controllerId,
        string partyId)
    {
        return reserveBuilder.GetOwnedReserves(mapEvent, controllerId, isHost: false)
            .SelectMany(side => side.Parties)
            .Single(party => party.PartyId == partyId)
            .InitialSpawnCount;
    }
}
