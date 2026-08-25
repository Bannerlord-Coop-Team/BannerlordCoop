using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Services.SiegeEvents;
using Moq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class BattleMissionStartHandlerTests : IDisposable
{
    private const string MapEventId = "map-event-1";
    private const string InitiatingPartyId = "attacker-party-1";

    private static readonly FieldInfo SidesField =
        typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo BattlePartiesField =
        typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo MapEventField =
        typeof(MapEventSide).GetField("_mapEvent", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly MobileParty? previousMainParty;

    static BattleMissionStartHandlerTests()
    {
        GameBootStrap.Initialize();
    }

    public BattleMissionStartHandlerTests()
    {
        previousMainParty = Campaign.Current?.MainParty;
    }

    public void Dispose()
    {
        if (Campaign.Current != null)
            Campaign.Current.MainParty = previousMainParty;
    }

    // The two missionOpen: true rows prove Clear wins regardless of battleStillValid, guarding
    // against a future reorder that would let a stale/invalid battle override an opened mission.
    [Theory]
    [InlineData(false, true, BattleMissionStartHandler.DeferredOpenAction.Keep)]
    [InlineData(false, false, BattleMissionStartHandler.DeferredOpenAction.Abandon)]
    [InlineData(true, false, BattleMissionStartHandler.DeferredOpenAction.Clear)]
    [InlineData(true, true, BattleMissionStartHandler.DeferredOpenAction.Clear)]
    public void DecideDeferredOpenAction_ResolvesByMissionAndBattleState(
        bool missionOpen, bool battleStillValid, object expected)
    {
        Assert.Equal((BattleMissionStartHandler.DeferredOpenAction)expected,
            BattleMissionStartHandler.DecideDeferredOpenAction(missionOpen, battleStillValid));
    }

    [Fact]
    public void GetOrCreateMissionInitializerSnapshot_ReusesFirstBattleInitializer()
    {
        using var messageBroker = new MessageBroker();
        using var handler = new BattleMissionStartHandler(
            messageBroker,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var initial = new MissionInitializerRecord("battle_terrain_026")
        {
            RandomTerrainSeed = 1234,
        };
        var later = new MissionInitializerRecord("battle_terrain_030")
        {
            RandomTerrainSeed = 5678,
        };

        var first = handler.GetOrCreateMissionInitializerSnapshot("map-event-1", () => initial);
        var repeated = handler.GetOrCreateMissionInitializerSnapshot("map-event-1", () => later);
        var otherBattle = handler.GetOrCreateMissionInitializerSnapshot("map-event-2", () => later);

        Assert.Equal("battle_terrain_026", first.SceneName);
        Assert.Equal(first.SceneName, repeated.SceneName);
        Assert.Equal(1234, repeated.RandomTerrainSeed);
        Assert.Equal("battle_terrain_030", otherBattle.SceneName);
    }

    [Fact]
    public void PendingBattle_DisposeDropsLoadingWindowAndGuardsDeferredCallback()
    {
        var window = new Mock<IGlobalLoadingWindow>();
        var barrier = CreatePendingBarrier(out var capturedOpen);
        using var messageBroker = new MessageBroker();
        var handler = CreateHandler(messageBroker, CreateObjectManager(), barrier.Object, window.Object);
        SetupMainPartyBattle();

        DriveAttackMissionStart(messageBroker);

        Assert.Equal(MapEventId, handler.DeferredAttackMissionMapEventId);
        Assert.NotNull(capturedOpen.Value);
        window.Verify(w => w.Enable(), Times.Once);
        window.Verify(w => w.Disable(), Times.Never);

        handler.Dispose();

        Assert.Null(handler.DeferredAttackMissionMapEventId);
        window.Verify(w => w.Disable(), Times.Once);

        // The barrier callback fires after disposal; the disposed guard makes it a no-op, so the
        // window is not touched again and no mission is opened on the dead handler.
        capturedOpen.Value();

        Assert.Null(handler.DeferredAttackMissionMapEventId);
        window.Verify(w => w.Disable(), Times.Once);
    }

    [Fact]
    public void PendingBattle_CommitRunsDeferredOpenInsteadOfImmediateOpen()
    {
        var window = new Mock<IGlobalLoadingWindow>();
        var barrier = CreatePendingBarrier(out var capturedOpen);
        using var messageBroker = new MessageBroker();
        using var handler = CreateHandler(messageBroker, CreateObjectManager(), barrier.Object, window.Object);
        SetupMainPartyBattle();

        DriveAttackMissionStart(messageBroker);

        Assert.Equal(MapEventId, handler.DeferredAttackMissionMapEventId);
        window.Verify(w => w.Enable(), Times.Once);
        barrier.Verify(b => b.RunAfterCommit(It.IsAny<MapEvent>(), It.IsAny<Action>()), Times.Once);
        // The open was registered on the barrier, not opened immediately, so the window is still held.
        window.Verify(w => w.Disable(), Times.Never);

        // The battle ends before the queued open runs (the race TryGetValidBattle guards); the commit
        // callback still fires and drives the open, which re-validates and drops the window.
        Campaign.Current.MainParty = null;
        capturedOpen.Value();

        Assert.Null(handler.DeferredAttackMissionMapEventId);
        window.Verify(w => w.Disable(), Times.Once);
    }

    private static Mock<IMapEventInitializationBarrier> CreatePendingBarrier(out DeferredOpen capturedOpen)
    {
        var open = new DeferredOpen();
        capturedOpen = open;
        var barrier = new Mock<IMapEventInitializationBarrier>();
        barrier.Setup(b => b.IsPending(It.IsAny<MapEvent>())).Returns(true);
        barrier.Setup(b => b.RunAfterCommit(It.IsAny<MapEvent>(), It.IsAny<Action>()))
            .Callback<MapEvent, Action>((_, action) => open.Value = action);
        return barrier;
    }

    private static IObjectManager CreateObjectManager()
    {
        var objectManager = new Mock<IObjectManager>();
        string mapEventId = MapEventId;
        objectManager.Setup(o => o.TryGetId(It.IsAny<object>(), out mapEventId)).Returns(true);
        return objectManager.Object;
    }

    private static BattleMissionStartHandler CreateHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IMapEventInitializationBarrier barrier,
        IGlobalLoadingWindow window)
    {
        return new BattleMissionStartHandler(
            messageBroker,
            objectManager,
            null!,
            null!,
            null!,
            null!,
            barrier,
            window);
    }

    private static void DriveAttackMissionStart(IMessageBroker messageBroker)
    {
        var message = new NetworkStartAttackMission(
            MapEventId,
            new MissionInitializerRecord("battle_terrain_026"),
            InitiatingPartyId);

        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            messageBroker.Publish(null, message);
        }
        finally
        {
            GameThread.Instance.RestoreGameThread(previousGameThreadId);
        }
    }

    private static void SetupMainPartyBattle()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var attackerSide = CreateSide(mapEvent, CreateMapEventParty(playerParty));
        var defenderSide = CreateSide(mapEvent, CreateMapEventParty(enemyParty));
        SidesField.SetValue(mapEvent, new[] { defenderSide, attackerSide });
        playerParty.Party._mapEventSide = attackerSide;
        Campaign.Current.MainParty = playerParty;
    }

    private static MapEventSide CreateSide(MapEvent mapEvent, params MapEventParty[] parties)
    {
        var side = ObjectHelper.SkipConstructor<MapEventSide>();
        BattlePartiesField.SetValue(side, new MBList<MapEventParty>(parties));
        MapEventField.SetValue(side, mapEvent);
        return side;
    }

    private static MapEventParty CreateMapEventParty(MobileParty mobileParty)
    {
        var mapEventParty = ObjectHelper.SkipConstructor<MapEventParty>();
        mapEventParty.Party = mobileParty.Party;
        return mapEventParty;
    }

    private static MobileParty CreateMobileParty()
    {
        var mobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        party.MobileParty = mobileParty;
        mobileParty.Party = party;
        return mobileParty;
    }

    private sealed class DeferredOpen
    {
        public Action Value { get; set; }
    }
}
