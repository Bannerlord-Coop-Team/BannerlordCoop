using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
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
            refresher);

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
