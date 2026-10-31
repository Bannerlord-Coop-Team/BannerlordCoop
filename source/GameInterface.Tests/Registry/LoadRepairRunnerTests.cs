using Common.Messaging;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.Clans;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Registry;

public class LoadRepairRunnerTests
{
    private const string CheckPrefix = "[Check] id=coop.boot.load-repairs ";

    private readonly CapturingLogger logger = new();
    private readonly MessageBroker messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IRegistryManager> registryManager = new();
    private readonly Mock<ILoadRepairRegistryReconciler> reconciler = new();
    private readonly Mock<IMapEventLoadCleaner> cleaner = new();
    private readonly Mock<ILeaderlessClanResolver> clanResolver = new();
    private readonly Mock<IEmptyLordPartyResetter> partyResetter = new();

    public LoadRepairRunnerTests()
    {
        reconciler.Setup(r => r.Capture()).Returns(LoadRepairSnapshot.Empty);
    }

    [Fact]
    public void RunLoadRepairs_AllStepsSucceed_LogsAPassingCheckLine()
    {
        objectManager.SetupSequence(manager => manager.Count).Returns(40).Returns(40);

        CreateRunner().RunLoadRepairs();

        var check = Assert.Single(logger.Events, IsCheckLine);
        Assert.Equal(LogEventLevel.Information, check.Level);
        Assert.Equal(
            "[Check] id=coop.boot.load-repairs result=pass stages_failed=0 unregistered_parties=0 " +
            "unregistered_party_ai=0 unregistered_armies=0 unregistered_equipment=0 unregistered_map_events=0 " +
            "unregistered_siege_events=0 unregistered_besieger_camps=0 unregistered_ships=0 announced_map_events=0 " +
            "registered_new=0",
            check.RenderMessage());
    }

    [Fact]
    public void RunLoadRepairs_AStepThrows_LogsAFailingCheckLineWithWhatThePassPublished()
    {
        objectManager.SetupSequence(manager => manager.Count).Returns(10).Returns(13);
        cleaner.Setup(c => c.CleanUpLoadedMapEvents()).Throws(new InvalidOperationException("cleaner failed"));
        reconciler.Setup(r => r.UnregisterRemoved(It.IsAny<LoadRepairSnapshot>())).Callback(() =>
        {
            messageBroker.Publish(this, new InstanceDestroyed<MobileParty>(null!));
            messageBroker.Publish(this, new InstanceDestroyed<MobileParty>(null!));
            messageBroker.Publish(this, new InstanceDestroyed<MobilePartyAi>(null!));
            messageBroker.Publish(this, new InstanceDestroyed<Ship>(null!));
            messageBroker.Publish(this, new MapEventFinalized(null!));
        });

        CreateRunner().RunLoadRepairs();

        // the failed stage is still logged on its own
        Assert.Single(logger.Events, logEvent => logEvent.Level == LogEventLevel.Error && logEvent.Exception is InvalidOperationException);
        var check = Assert.Single(logger.Events, IsCheckLine);
        Assert.Equal(LogEventLevel.Information, check.Level);
        Assert.Equal(
            "[Check] id=coop.boot.load-repairs result=fail stages_failed=1 unregistered_parties=2 " +
            "unregistered_party_ai=1 unregistered_armies=0 unregistered_equipment=0 unregistered_map_events=0 " +
            "unregistered_siege_events=0 unregistered_besieger_camps=0 unregistered_ships=1 announced_map_events=1 " +
            "registered_new=3",
            check.RenderMessage());
    }

    private static bool IsCheckLine(LogEvent logEvent) => logEvent.RenderMessage().StartsWith(CheckPrefix, StringComparison.Ordinal);

    private LoadRepairRunner CreateRunner() => new(
        logger,
        messageBroker,
        objectManager.Object,
        registryManager.Object,
        reconciler.Object,
        cleaner.Object,
        clanResolver.Object,
        partyResetter.Object);

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEvent> Events { get; } = new();

        public void Write(LogEvent logEvent) => Events.Add(logEvent);
    }
}
