using Common.Messaging;
using E2E.Tests.Util;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class HideoutTroopSelectionTests : MissionTestEnvironment
{
    public HideoutTroopSelectionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(10, 4)]
    public void SharedAllowance_ExcludesEachPlayerHeroAndFiltersTheirReserves(int firstCount, int joiningCount)
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender", "joining");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            var joining = PreparePlayer("joining", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, firstCount), out _, out var remaining));
            Assert.Equal(14 - firstCount, remaining);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, joining.Party, "joining", 39,
                Troops(joining.TroopId, joiningCount), out var accepted, out remaining));
            Assert.Equal(0, remaining);
            Assert.Equal(joining.HeroId, accepted[0].CharacterId);
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);

            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var firstSide = builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker);
            var joiningSide = builder.GetOwnedReserves(setup.MapEvent, "joining", false)
                .Single(side => side.Side == BattleSideEnum.Attacker);
            Assert.Equal(16, firstSide.TotalTroops);
            Assert.Equal(16, joiningSide.TotalTroops);
            var firstReserve = Assert.Single(firstSide.Parties);
            var joiningReserve = Assert.Single(joiningSide.Parties);
            Assert.Equal(firstCount + 1, firstReserve.Entries.Length);
            Assert.Equal(joiningCount + 1, joiningReserve.Entries.Length);
            Assert.Equal(first.HeroId, firstReserve.Entries[0].CharacterId);
            Assert.Equal(joining.HeroId, joiningReserve.Entries[0].CharacterId);
        });
    }

    [Fact]
    public void StaleSimultaneousSelection_IsRejectedWithoutSpendingRemainingSlots()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender", "joining");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            var joining = PreparePlayer("joining", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 10), out _, out _));
            Assert.False(setup.Selection.TrySelect(setup.Settlement, joining.Party, "joining", 14,
                Troops(joining.TroopId, 10), out var accepted, out var remaining));
            Assert.Empty(accepted);
            Assert.Equal(4, remaining);
            Assert.False(setup.Selection.HasSelection(setup.Settlement, joining.Party));
            Assert.True(setup.Selection.TrySelect(setup.Settlement, joining.Party, "joining", 14,
                Troops(joining.TroopId, 4), out _, out remaining));
            Assert.Equal(0, remaining);
        });
    }

    [Fact]
    public void Companions_ConsumeEscortSlotsButPlayerHeroDoesNot()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            first.Party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            Assert.True(Server.ObjectManager.TryGetId(companion.CharacterObject, out var companionId));
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                new[] { new HideoutTroopSelectionEntry(first.HeroId, 1),
                    new HideoutTroopSelectionEntry(companionId, 1), new HideoutTroopSelectionEntry(first.TroopId, 13) },
                out var accepted, out var remaining));
            Assert.Equal(0, remaining);
            Assert.Equal(15, accepted.Sum(entry => entry.Count));
        });
    }

    [Fact]
    public void Selection_RejectsAnotherPlayersPartyAndUnavailableOrWoundedTroops()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender", "joining");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 10, wounded: 3);
            var joining = PreparePlayer("joining", 10);
            Assert.False(setup.Selection.TrySelect(setup.Settlement, first.Party, "joining", 14,
                Troops(first.TroopId, 1), out _, out _));
            Assert.False(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(joining.TroopId, 1), out _, out _));
            Assert.False(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 8), out _, out _));
            Assert.False(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                new[] { new HideoutTroopSelectionEntry(first.TroopId, 4),
                    new HideoutTroopSelectionEntry(first.TroopId, 4) }, out _, out _));
            Assert.Equal(14, setup.Selection.GetRemaining(setup.Settlement, 14));
        });
    }

    [Fact]
    public void RepeatSelection_AfterCasualtiesCannotExpandOrReleaseAnAllocation()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 10), out var original, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            first.Party.MemberRoster.AddToCounts(first.Troop, -6);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 39,
                Troops(first.TroopId, 14), out var repeated, out var remaining));
            Assert.Equal(original, repeated);
            Assert.Equal(4, remaining);
            setup.Selection.CancelUnbound(setup.Settlement, first.Party);
            Assert.Equal(4, setup.Selection.GetRemaining(setup.Settlement, 14));
        });
    }

    [Fact]
    public void Retreat_PreservesSuppliedPointerAndCannotReplenishEscortReserve()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var ledger = Server.Resolve<IBattleTroopLedger>();
            var reserve = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            ledger.ReportSupplied(mapEventId, reserve.PartyId, 15);
            builder.ForgetController(setup.MapEvent, "first");
            first.Party.MemberRoster.AddToCounts(first.Troop, 20);
            var repeated = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            Assert.Equal(15, repeated.SuppliedCount);
            Assert.Equal(reserve.Entries, repeated.Entries);
            Assert.Empty(ledger.GetRemaining(mapEventId, reserve.PartyId));
        });
    }

    [Fact]
    public void RejoiningWithNewMapEventParty_PreservesSpentDescriptors()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender", "joining");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            var joining = PreparePlayer("joining", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            Assert.True(setup.Selection.TrySelect(setup.Settlement, joining.Party, "joining", 14,
                Array.Empty<HideoutTroopSelectionEntry>(), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var ledger = Server.Resolve<IBattleTroopLedger>();
            var original = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            ledger.ReportSupplied(mapEventId, original.PartyId, 15);
            builder.ForgetController(setup.MapEvent, "first");
            first.Party.Party.MapEventSide = null;
            var mainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = null;
            try
            {
                using (new BarterPlayerContext(first.Party.MemberRoster.GetTroopRoster()
                    .Single(entry => entry.Character.IsHero).Character.HeroObject, first.Party))
                    first.Party.Party.MapEventSide = setup.MapEvent.AttackerSide;
                Assert.Null(Campaign.Current.MainParty);
            }
            finally
            {
                Campaign.Current.MainParty = mainParty;
            }
            var repeated = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            Assert.NotEqual(original.PartyId, repeated.PartyId);
            Assert.Equal(original.Entries, repeated.Entries);
            Assert.Equal(15, repeated.SuppliedCount);
            Assert.Empty(ledger.GetRemaining(mapEventId, repeated.PartyId));
        });
    }

    [Fact]
    public void LiveMissionReset_PreservesHideoutAdmissionsAndSpentReserve()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var ledger = Server.Resolve<IBattleTroopLedger>();
            var original = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            ledger.ReportSupplied(mapEventId, original.PartyId, 15);

            builder.ForgetMapEvent(setup.MapEvent);

            Assert.True(setup.Selection.HasSelection(setup.Settlement, first.Party));
            Assert.Equal(0, setup.Selection.GetRemaining(setup.Settlement, 14));
            var repeated = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "first", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties);
            Assert.Equal(original.Entries, repeated.Entries);
            Assert.Equal(15, repeated.SuppliedCount);

            setup.MapEvent.State = MapEventState.WaitingRemoval;
            Server.Resolve<IMessageBroker>().Publish(this, new MapEventFinalized(setup.MapEvent));
            Assert.False(setup.Selection.HasSelection(setup.Settlement, first.Party));
            Assert.Empty(ledger.GetParties(mapEventId));
        });
    }

    [Fact]
    public void FinalizedHideout_WithClearedSides_RemovesDefenderFlattenCache()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var ledger = Server.Resolve<IBattleTroopLedger>();
            var defender = Assert.Single(builder.GetOwnedReserves(setup.MapEvent, "defender", false)
                .Single(side => side.Side == BattleSideEnum.Defender).Parties);
            var builtParties = Assert.IsType<HashSet<string>>(typeof(BattleTroopReserveBuilder)
                .GetField("builtParties", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(builder));
            Assert.Contains(defender.PartyId, builtParties);

            setup.MapEvent.State = MapEventState.WaitingRemoval;
            setup.MapEvent.AttackerSide.Clear();
            setup.MapEvent.DefenderSide.Clear();
            Server.Resolve<IMessageBroker>().Publish(this, new MapEventFinalized(setup.MapEvent));

            Assert.Empty(builtParties);
            Assert.Empty(ledger.GetParties(mapEventId));
            Assert.False(setup.Selection.HasSelection(setup.Settlement, first.Party));
        });
    }

    [Fact]
    public void CancelFailedPreparation_AndFinalizedMapEventReleaseTheirAttempt()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            setup.Selection.CancelUnbound(setup.Settlement, first.Party);
            Assert.False(setup.Selection.HasSelection(setup.Settlement, first.Party));
            Assert.Equal(39, setup.Selection.GetRemaining(setup.Settlement, 39));
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 39,
                Troops(first.TroopId, 20), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            Server.Resolve<IMessageBroker>().Publish(this, new MapEventFinalized(setup.MapEvent));
            Assert.False(setup.Selection.HasSelection(setup.Settlement, first.Party));
            Assert.Equal(14, setup.Selection.GetRemaining(setup.Settlement, 14));
        });
    }

    [Fact]
    public void UnselectedPlayerParty_HasNoAttackerReserveUntilSelectionIsAccepted()
    {
        var (mapEventId, _) = SetupCoopBattle("first", "defender", "joining");
        Server.Call(() =>
        {
            var setup = PrepareHideout(mapEventId);
            var first = PreparePlayer("first", 20);
            var joining = PreparePlayer("joining", 20);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, first.Party, "first", 14,
                Troops(first.TroopId, 14), out _, out _));
            setup.Selection.BindMapEvent(setup.Settlement, setup.MapEvent);
            var side = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(setup.MapEvent, "joining", false)
                .Single(reserve => reserve.Side == BattleSideEnum.Attacker);
            Assert.Empty(side.Parties);
            Assert.Equal(15, side.TotalTroops);
            Assert.True(setup.Selection.TrySelect(setup.Settlement, joining.Party, "joining", 14,
                Array.Empty<HideoutTroopSelectionEntry>(), out _, out _));
            side = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(setup.MapEvent, "joining", false)
                .Single(reserve => reserve.Side == BattleSideEnum.Attacker);
            Assert.Equal(joining.HeroId, Assert.Single(Assert.Single(side.Parties).Entries).CharacterId);
            Assert.Equal(16, side.TotalTroops);
        });
    }

    private (Settlement Settlement, MapEvent MapEvent, IHideoutTroopSelection Selection) PrepareHideout(string mapEventId)
    {
        Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
        mapEvent.MapEventSettlement = settlement;
        mapEvent._mapEventType = MapEvent.BattleTypes.Hideout;
        return (settlement, mapEvent, Server.Resolve<IHideoutTroopSelection>());
    }

    private (MobileParty Party, string HeroId, CharacterObject Troop, string TroopId) PreparePlayer(
        string controllerId, int troops, int wounded = 0)
    {
        var players = Server.Resolve<IPlayerManager>();
        Assert.True(players.TryGetPlayer(controllerId, out var player));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
        Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var heroId));
        Assert.True(players.ReplacePlayer(player,
            new Player(player.ControllerId, player.HeroId, player.MobilePartyId, player.ClanId, heroId)));
        var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));
        party.MemberRoster.Clear();
        party.MemberRoster.AddToCounts(troop, troops, woundedCount: wounded);
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        party.MapEvent.FindMapEventParty(party.Party).Update();
        return (party, heroId, troop, troopId);
    }

    private static HideoutTroopSelectionEntry[] Troops(string troopId, int count)
        => count == 0 ? Array.Empty<HideoutTroopSelectionEntry>() : new[] { new HideoutTroopSelectionEntry(troopId, count) };
}
