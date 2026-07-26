using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.TroopRosters.Handlers;
using GameInterface.Services.TroopRosters.Messages;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.TroopRosters;

public class TroopRosterDeltaHandlerRefreshTests
{
    static TroopRosterDeltaHandlerRefreshTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void NetworkElementBatch_RefreshesEncounterConditionsAfterRosterApply()
    {
        var roster = new TroopRoster();
        var character = new CharacterObject();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging("roster", out roster)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging("character", out character)).Returns(true);

        Action<MessagePayload<NetworkTroopRosterElementBatch>>? subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkTroopRosterElementBatch>>>()!))
            .Callback<Action<MessagePayload<NetworkTroopRosterElementBatch>>>(handler => subscriber = handler);

        using var refreshed = new ManualResetEventSlim(false);
        var refresher = new RecordingEncounterMenuConditionRefresher(refreshed);

        using var handler = new TroopRosterDeltaHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object,
            refresher,
            new NullPartyScreenRosterBaselineProvider());

        Assert.NotNull(subscriber);
        subscriber!(new MessagePayload<NetworkTroopRosterElementBatch>(
            this,
            new NetworkTroopRosterElementBatch(
                "roster",
                "character",
                new[]
                {
                    TroopRosterElementOperation.AddCounts(3, 0, 0, false),
                    TroopRosterElementOperation.AddCounts(-2, 0, 0, false),
                })));

        Assert.True(refreshed.Wait(TimeSpan.FromSeconds(10)), "roster apply did not refresh encounter conditions");
        Assert.Equal(1, roster.TotalHealthyCount);
        Assert.Same(roster, refresher.RefreshedRoster);
        Assert.Equal(1, refresher.HealthyCountAtRefresh);
        Assert.Equal(1, refresher.RefreshCount);
    }

    [Fact]
    public void NetworkRemoval_UpdatesPartyScreenBaseline()
    {
        var roster = new TroopRoster();
        var baselineRoster = new TroopRoster();
        var character = new CharacterObject();
        roster.AddToCounts(character, 1);
        baselineRoster.AddToCounts(character, 1);

        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging("roster", out roster)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging("character", out character)).Returns(true);

        Action<MessagePayload<NetworkTroopRosterElementBatch>>? subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkTroopRosterElementBatch>>>()!))
            .Callback<Action<MessagePayload<NetworkTroopRosterElementBatch>>>(handler => subscriber = handler);

        using var refreshed = new ManualResetEventSlim(false);
        using var handler = new TroopRosterDeltaHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object,
            new RecordingEncounterMenuConditionRefresher(refreshed),
            new FixedPartyScreenRosterBaselineProvider(roster, baselineRoster));

        subscriber!(new MessagePayload<NetworkTroopRosterElementBatch>(
            this,
            new NetworkTroopRosterElementBatch(
                "roster",
                "character",
                new[] { TroopRosterElementOperation.AddCounts(-1, 0, 0, true) })));

        Assert.True(refreshed.Wait(TimeSpan.FromSeconds(10)), "roster apply did not complete");
        Assert.Equal(0, roster.GetTroopCount(character));
        Assert.Equal(0, baselineRoster.GetTroopCount(character));
    }

    [Fact]
    public void AbsoluteZero_RemovesStalePartyScreenBaselineWhenLiveRosterIsEmpty()
    {
        var roster = new TroopRoster();
        var baselineRoster = new TroopRoster();
        var character = new CharacterObject();
        baselineRoster.AddToCounts(character, 1);

        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging("roster", out roster)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging("character", out character)).Returns(true);

        Action<MessagePayload<NetworkTroopRosterSetNumber>>? setNumberSubscriber = null;
        Action<MessagePayload<NetworkTroopRosterRemoveZeroCounts>>? removeZeroSubscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkTroopRosterSetNumber>>>()!))
            .Callback<Action<MessagePayload<NetworkTroopRosterSetNumber>>>(handler => setNumberSubscriber = handler);
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkTroopRosterRemoveZeroCounts>>>()!))
            .Callback<Action<MessagePayload<NetworkTroopRosterRemoveZeroCounts>>>(handler => removeZeroSubscriber = handler);

        using var refreshed = new ManualResetEventSlim(false);
        using var handler = new TroopRosterDeltaHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object,
            new RecordingEncounterMenuConditionRefresher(refreshed),
            new FixedPartyScreenRosterBaselineProvider(roster, baselineRoster));

        setNumberSubscriber!(
            new MessagePayload<NetworkTroopRosterSetNumber>(
                this,
                new NetworkTroopRosterSetNumber("roster", "character", 0)));
        Assert.True(refreshed.Wait(TimeSpan.FromSeconds(10)), "absolute roster correction did not complete");
        Assert.Equal(0, baselineRoster.GetTroopCount(character));

        refreshed.Reset();
        removeZeroSubscriber!(
            new MessagePayload<NetworkTroopRosterRemoveZeroCounts>(
                this,
                new NetworkTroopRosterRemoveZeroCounts("roster")));

        Assert.True(refreshed.Wait(TimeSpan.FromSeconds(10)), "zero-count removal did not complete");
        Assert.Equal(-1, baselineRoster.FindIndexOfTroop(character));
    }

    private sealed class NullPartyScreenRosterBaselineProvider : IPartyScreenRosterBaselineProvider
    {
        public TroopRoster GetBaselineRoster(TroopRoster candidate) => null!;
    }

    private sealed class FixedPartyScreenRosterBaselineProvider : IPartyScreenRosterBaselineProvider
    {
        private readonly TroopRoster roster;
        private readonly TroopRoster baselineRoster;

        public FixedPartyScreenRosterBaselineProvider(TroopRoster roster, TroopRoster baselineRoster)
        {
            this.roster = roster;
            this.baselineRoster = baselineRoster;
        }

        public TroopRoster GetBaselineRoster(TroopRoster candidate)
            => ReferenceEquals(candidate, roster) ? baselineRoster : null!;
    }

    private sealed class RecordingEncounterMenuConditionRefresher : IEncounterMenuConditionRefresher
    {
        private readonly ManualResetEventSlim refreshed;

        public TroopRoster? RefreshedRoster { get; private set; }
        public int HealthyCountAtRefresh { get; private set; }
        public int RefreshCount { get; private set; }

        public RecordingEncounterMenuConditionRefresher(ManualResetEventSlim refreshed)
        {
            this.refreshed = refreshed;
        }

        public void RefreshForMapEvent(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
        }

        public void RefreshForRoster(TroopRoster roster)
        {
            RefreshedRoster = roster;
            HealthyCountAtRefresh = roster.TotalHealthyCount;
            RefreshCount++;
            refreshed.Set();
        }
    }
}
