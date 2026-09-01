using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.Telemetry;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

public class BattlesFoughtReporterTests
{
    [Fact]
    public void PlayerJoinedBattle_ReportsEachMapEventOnce()
    {
        var messageBroker = new TestMessageBroker();
        var objectManager = new Mock<IObjectManager>();
        var uploader = new RecordingBattlesFoughtUploader();
        using var sessionCancellation = new CancellationTokenSource();
        var firstBattle = ObjectHelper.SkipConstructor<MapEvent>();
        var secondBattle = ObjectHelper.SkipConstructor<MapEvent>();
        string firstBattleId = "map-event-1";
        string secondBattleId = "map-event-2";
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(firstBattle, out firstBattleId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(secondBattle, out secondBattleId))
            .Returns(true);
        using var reporter = new BattlesFoughtReporter(
            messageBroker,
            objectManager.Object,
            uploader,
            sessionCancellation);

        messageBroker.Publish(firstBattle, new PlayerJoinedBattle());
        messageBroker.Publish(firstBattle, new PlayerJoinedBattle());
        messageBroker.Publish(secondBattle, new PlayerJoinedBattle());

        Assert.Equal(2, uploader.ReportCount);
    }

    private sealed class RecordingBattlesFoughtUploader : IBattlesFoughtUploader
    {
        public int ReportCount { get; private set; }

        public Task<ServerTelemetryUploadResult> RecordBattleStartedAsync(
            CancellationToken cancellationToken)
        {
            ReportCount++;
            return Task.FromResult(new ServerTelemetryUploadResult(true, true, "accepted"));
        }
    }
}
