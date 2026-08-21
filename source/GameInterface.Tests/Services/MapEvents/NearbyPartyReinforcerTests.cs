using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class NearbyPartyReinforcerTests : IDisposable
{
    static NearbyPartyReinforcerTests()
    {
        GameBootStrap.Initialize();
    }

    private static readonly FieldInfo SidesField =
        typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo BattlePartiesField =
        typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo MapEventField =
        typeof(MapEventSide).GetField("_mapEvent", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo NearbyPartiesField =
        typeof(MapEventSide).GetField("_nearbyPartiesAddedToPlayerMapEvent", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
    private readonly List<object> controlledObjects = new();
    private readonly bool previousIsServer;

    public NearbyPartyReinforcerTests()
    {
        previousIsServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
    }

    public void Dispose()
    {
        foreach (var controlledObject in controlledObjects)
            playerObjects.Remove(controlledObject);

        ModInformation.IsServer = previousIsServer;
    }

    [Fact]
    public void PlayerBattleStart_ImmediatelyAddsSelectedNearbyAlly()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var nearbyAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var joins = new List<(MapEventSide Side, MobileParty Party)>();
        var reinforcer = CreateReinforcer();
        var previousMainParty = Campaign.Current.MainParty;
        var previousEncounter = Campaign.Current.PlayerEncounter;

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) =>
            {
                Assert.Same(playerParty, MobileParty.MainParty);
                Assert.Same(mapEvent, PlayerEncounter.Battle);
                playerSide.Add(nearbyAlly);
            },
            (side, party) => joins.Add((side, party)));

        Assert.Same(previousMainParty, Campaign.Current.MainParty);
        Assert.Same(previousEncounter, Campaign.Current.PlayerEncounter);
        var join = Assert.Single(joins);
        Assert.Same(mapEvent.AttackerSide, join.Side);
        Assert.Same(nearbyAlly, join.Party);
    }

    [Fact]
    public void SallyOutSelection_UsesMapEventSettlementWithoutPlayerSiege()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        var encounterSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        mapEvent._mapEventType = MapEvent.BattleTypes.SallyOut;
        mapEvent.MapEventSettlement = encounterSettlement;
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var selectorCalled = false;
        var reinforcer = CreateReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) =>
            {
                selectorCalled = true;
                Assert.Same(encounterSettlement, PlayerEncounter.EncounterSettlement);
            },
            (side, party) => { });

        Assert.True(selectorCalled);
    }

    [Fact]
    public void CampaignTick_AddsAllyThatArrivesLaterDuringWindow()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var lateAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var joins = new List<MobileParty>();
        var reinforcer = CreateReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (attackers, defenders) => { },
            (side, party) => joins.Add(party));

        long firstScanTicks = CampaignTime.Now.NumTicks;
        var selectorCalls = 0;
        Action<List<MobileParty>, List<MobileParty>> selectLateAlly = (attackers, defenders) =>
        {
            selectorCalls++;
            attackers.Add(lateAlly);
        };

        reinforcer.ReinforceOpenPlayerBattles(
            firstScanTicks,
            selectLateAlly,
            (side, party) => joins.Add(party));

        Assert.Equal(0, selectorCalls);
        Assert.Empty(joins);

        reinforcer.ReinforceOpenPlayerBattles(
            firstScanTicks + NearbyPartyReinforcer.FollowUpScanIntervalTicks,
            selectLateAlly,
            (side, party) => joins.Add(party));

        Assert.Equal(1, selectorCalls);
        Assert.Collection(joins, party => Assert.Same(lateAlly, party));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutsideWindowOrFinalizedEvent_DoesNotAddJoiner(bool finalized)
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var nearbyAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        if (finalized)
        {
            InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
            mapEvent._state = MapEventState.WaitingRemoval;
        }
        var selectorCalled = false;
        var joins = new List<MobileParty>();
        var reinforcer = CreateReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (attackers, defenders) =>
            {
                selectorCalled = true;
                attackers.Add(nearbyAlly);
            },
            (side, party) => joins.Add(party));

        Assert.False(selectorCalled);
        Assert.Empty(joins);
    }

    [Fact]
    public void ConcludedEvent_DoesNotSelectJoiners()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        mapEvent._battleState = TaleWorlds.Core.BattleState.AttackerVictory;
        var selectorCalled = false;
        var reinforcer = CreateReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) => selectorCalled = true,
            (side, party) => { });

        Assert.False(selectorCalled);
    }

    [Fact]
    public void EventConcludedDuringSelection_DoesNotAddJoiner()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var nearbyAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var joins = new List<MobileParty>();
        var reinforcer = CreateReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) =>
            {
                playerSide.Add(nearbyAlly);
                mapEvent._battleState = TaleWorlds.Core.BattleState.AttackerVictory;
            },
            (side, party) => joins.Add(party));

        Assert.Empty(joins);
    }

    [Fact]
    public void PlayersLeaveInOrder_RemovesReinforcementsOnlyAfterLastPlayer()
    {
        var firstPlayer = CreateMobileParty();
        var lastPlayer = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(firstPlayer, enemyParty);
        AddMapEventParty(mapEvent.AttackerSide, lastPlayer);
        MarkAsPlayerParty(firstPlayer);
        MarkAsPlayerParty(lastPlayer);
        var cleanedSides = new List<MapEventSide>();
        var reinforcer = CreateReinforcer();

        RemoveMapEventParty(mapEvent.AttackerSide, firstPlayer);
        reinforcer.RemoveReinforcementsIfNoPlayers(mapEvent, firstPlayer, cleanedSides.Add);

        Assert.Empty(cleanedSides);

        RemoveMapEventParty(mapEvent.AttackerSide, lastPlayer);
        reinforcer.RemoveReinforcementsIfNoPlayers(mapEvent, lastPlayer, cleanedSides.Add);

        Assert.Collection(
            cleanedSides,
            side => Assert.Same(mapEvent.AttackerSide, side),
            side => Assert.Same(mapEvent.DefenderSide, side));
    }

    [Fact]
    public void TrackedReinforcementLeavesBeforePlayers_RemovesStaleTracking()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var removedReinforcement = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        mapEvent.AttackerSide._nearbyPartiesAddedToPlayerMapEvent.Add(removedReinforcement);
        var cleanedSides = new List<MapEventSide>();
        var reinforcer = CreateReinforcer();

        reinforcer.RemoveReinforcementsIfNoPlayers(
            mapEvent,
            removedReinforcement,
            cleanedSides.Add);

        Assert.Empty(mapEvent.AttackerSide._nearbyPartiesAddedToPlayerMapEvent);
        Assert.Empty(cleanedSides);
    }

    [Fact]
    public void PlayerRejoinsAfterCleanup_LastPlayerCleanupRunsAgain()
    {
        var firstPlayer = CreateMobileParty();
        var rejoiningPlayer = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(firstPlayer, enemyParty);
        MarkAsPlayerParty(firstPlayer);
        MarkAsPlayerParty(rejoiningPlayer);
        var cleanedSides = new List<MapEventSide>();
        var reinforcer = CreateReinforcer();

        RemoveMapEventParty(mapEvent.AttackerSide, firstPlayer);
        reinforcer.RemoveReinforcementsIfNoPlayers(mapEvent, firstPlayer, cleanedSides.Add);
        AddMapEventParty(mapEvent.AttackerSide, rejoiningPlayer);
        RemoveMapEventParty(mapEvent.AttackerSide, rejoiningPlayer);
        reinforcer.RemoveReinforcementsIfNoPlayers(mapEvent, rejoiningPlayer, cleanedSides.Add);

        Assert.Equal(4, cleanedSides.Count);
    }

    [Fact]
    public void AttachedArmyCleanup_PublishesEveryRecursivelyRemovedParty()
    {
        var removedPlayer = CreateMobileParty();
        var nearbyArmyLeader = CreateMobileParty();
        var attachedArmyParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(nearbyArmyLeader, enemyParty);
        var side = mapEvent.AttackerSide;
        var leaderMapEventParty = side.Parties[0];
        var attachedMapEventParty = CreateMapEventParty(attachedArmyParty);
        side._battleParties.Add(attachedMapEventParty);
        MapEventField.SetValue(side, mapEvent);
        nearbyArmyLeader.Party._mapEventSide = side;
        attachedArmyParty.Party._mapEventSide = side;
        nearbyArmyLeader._attachedParties = new MBList<MobileParty> { attachedArmyParty };
        attachedArmyParty._attachedParties = new MBList<MobileParty>();
        attachedArmyParty._attachedTo = nearbyArmyLeader;
        var army = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
        army.LeaderParty = nearbyArmyLeader;
        nearbyArmyLeader._army = army;
        attachedArmyParty._army = army;
        // Keep the uninitialized fixture out of vanilla's post-removal movement reset.
        nearbyArmyLeader._isCurrentlyUsedByAQuest = true;
        side._nearbyPartiesAddedToPlayerMapEvent.Add(nearbyArmyLeader);
        MarkAsPlayerParty(removedPlayer);
        var publishedRemovals = new List<(object Source, MapEventPartyRemoved Message)>();
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(broker => broker.Publish(
                It.IsAny<object>(),
                It.IsAny<MapEventPartyRemoved>()))
            .Callback<object, MapEventPartyRemoved>((source, message) =>
                publishedRemovals.Add((source, message)));
        var reinforcer = new NearbyPartyReinforcer(messageBroker.Object);

        reinforcer.RemoveReinforcementsIfNoPlayers(mapEvent, removedPlayer);

        Assert.Null(nearbyArmyLeader.MapEventSide);
        Assert.Null(attachedArmyParty.MapEventSide);
        Assert.DoesNotContain(leaderMapEventParty, side.Parties);
        Assert.DoesNotContain(attachedMapEventParty, side.Parties);
        Assert.Collection(
            publishedRemovals,
            removal =>
            {
                Assert.Same(side, removal.Source);
                Assert.Same(leaderMapEventParty, removal.Message.MapEventParty);
            },
            removal =>
            {
                Assert.Same(side, removal.Source);
                Assert.Same(attachedMapEventParty, removal.Message.MapEventParty);
            });
    }

    [Fact]
    public void PartyRemovalAnnouncement_UsesHandlerOwnedReinforcerWithPatchesLive()
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        var removedParty = CreateMobileParty();
        using var reinforcer = new RecordingReinforcer(mapEvent, removedParty);
        using var messageBroker = new MessageBroker();
        using var handler = new NearbyPartyReinforcementHandler(messageBroker, reinforcer);

        bool ownsGameThreadMark = GameThread.Instance.GameThreadId == 0;
        if (ownsGameThreadMark)
            GameThread.Instance.MarkGameThread();

        try
        {
            using (new AllowedThread())
                messageBroker.Publish(mapEvent, new PartyRemovedFromMapEvent(removedParty));
        }
        finally
        {
            if (ownsGameThreadMark)
                GameThread.Instance.RestoreGameThread(0);
        }

        Assert.True(reinforcer.WaitForCleanup());
        Assert.Equal(1, reinforcer.CleanupCount);
    }

    [Fact]
    public void PlayerBattleAnnouncement_OpensWindowBeforeImmediateGameThreadScan()
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        using var reinforcer = new RecordingReinforcer(mapEvent);
        using var messageBroker = new MessageBroker();
        using var handler = new NearbyPartyReinforcementHandler(messageBroker, reinforcer);

        bool ownsGameThreadMark = GameThread.Instance.GameThreadId == 0;
        if (ownsGameThreadMark)
            GameThread.Instance.MarkGameThread();

        try
        {
            using (new AllowedThread())
            {
                InteractionPatches.OpenAiJoinWindowAndPublish(
                    mapEvent,
                    () => messageBroker.Publish(mapEvent, new PlayerJoinedBattle()));
            }
        }
        finally
        {
            if (ownsGameThreadMark)
                GameThread.Instance.RestoreGameThread(0);
        }

        Assert.True(reinforcer.WaitForImmediateScan());
        Assert.Equal(1, reinforcer.ImmediateScanCount);
    }

    private static NearbyPartyReinforcer CreateReinforcer()
        => new NearbyPartyReinforcer(Mock.Of<IMessageBroker>());

    private MapEvent CreatePlayerBattle(MobileParty playerParty, MobileParty enemyParty)
    {
        var attackerSide = CreateSide(CreateMapEventParty(playerParty));
        var defenderSide = CreateSide(CreateMapEventParty(enemyParty));
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        SidesField.SetValue(mapEvent, new[] { defenderSide, attackerSide });
        mapEvent._state = MapEventState.Wait;
        return mapEvent;
    }

    private static MapEventSide CreateSide(params MapEventParty[] parties)
    {
        var side = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
        BattlePartiesField.SetValue(side, new MBList<MapEventParty>(parties));
        NearbyPartiesField.SetValue(side, new MBList<MobileParty>());
        return side;
    }

    private static void AddMapEventParty(MapEventSide side, MobileParty mobileParty)
    {
        side._battleParties.Add(CreateMapEventParty(mobileParty));
    }

    private static void RemoveMapEventParty(MapEventSide side, MobileParty mobileParty)
    {
        for (int i = side._battleParties.Count - 1; i >= 0; i--)
        {
            if (side._battleParties[i].Party == mobileParty.Party)
                side._battleParties.RemoveAt(i);
        }
    }

    private static MapEventParty CreateMapEventParty(MobileParty mobileParty)
    {
        var mapEventParty = (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
        mapEventParty.Party = mobileParty.Party;
        return mapEventParty;
    }

    private static MobileParty CreateMobileParty()
    {
        var mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.MobileParty = mobileParty;
        mobileParty.Party = party;
        return mobileParty;
    }

    private void MarkAsPlayerParty(MobileParty mobileParty)
    {
        playerObjects.Add(
            mobileParty,
            new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));
        controlledObjects.Add(mobileParty);
    }

    private sealed class RecordingReinforcer : INearbyPartyReinforcer, IDisposable
    {
        private readonly MapEvent expectedMapEvent;
        private readonly MobileParty? expectedRemovedParty;
        private readonly ManualResetEventSlim immediateScanCompleted = new(false);
        private readonly ManualResetEventSlim cleanupCompleted = new(false);

        public RecordingReinforcer(MapEvent expectedMapEvent, MobileParty? expectedRemovedParty = null)
        {
            this.expectedMapEvent = expectedMapEvent;
            this.expectedRemovedParty = expectedRemovedParty;
        }

        public int ImmediateScanCount { get; private set; }
        public int CleanupCount { get; private set; }

        public void Reinforce(MapEvent mapEvent)
        {
            Assert.Same(expectedMapEvent, mapEvent);
            Assert.True(GameThread.Instance.IsGameThread);
            Assert.False(AllowedThread.IsThisThreadAllowed());
            Assert.True(InteractionPatches.IsWithinAiJoinWindow(mapEvent));
            ImmediateScanCount++;
            immediateScanCompleted.Set();
        }

        public void ReinforceOpenPlayerBattles()
        {
        }

        public void RemoveReinforcementsIfNoPlayers(MapEvent mapEvent, MobileParty removedParty)
        {
            Assert.Same(expectedMapEvent, mapEvent);
            Assert.Same(expectedRemovedParty, removedParty);
            Assert.True(GameThread.Instance.IsGameThread);
            Assert.False(AllowedThread.IsThisThreadAllowed());
            CleanupCount++;
            cleanupCompleted.Set();
        }

        public bool WaitForImmediateScan() => immediateScanCompleted.Wait(TimeSpan.FromSeconds(5));
        public bool WaitForCleanup() => cleanupCompleted.Wait(TimeSpan.FromSeconds(5));

        public void Dispose()
        {
            immediateScanCompleted.Dispose();
            cleanupCompleted.Dispose();
        }
    }
}
