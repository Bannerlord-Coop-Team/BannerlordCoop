#if DEBUG
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Heroes.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabLifecycleTests : NavalMissionTestEnvironment
{
    public NavalLabLifecycleTests(ITestOutputHelper output) : base(output) { }

    private void Start()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(First);
        Ready(Second);
        Tick(First);
        Tick(Second);
        Assert.Single(Actions(First), action => action.Kind == "release");
        Assert.Single(Actions(Second), action => action.Kind == "release");
    }

    [Fact]
    public void NativeStartupFailure_HoldsTheOtherClient_AndStopRemainsIdempotent()
    {
        Adapter(Second).ThrowOnOpen = true;
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(First);
        Assert.True(Adapter(Second).Disposed);
        Assert.False(Second.Resolve<MockBattleNetwork>().IsStarted);
        Assert.Contains("simulated native open failure", Receipt(Second, Manifest.IncarnationId));
        Assert.Single(Actions(First), action => action.Kind == "hold");
        Assert.DoesNotContain(Actions(First), action => action.Kind == "release");
        Assert.Throws<InvalidOperationException>(() => Execute("native-take-helm"));
        Guid stop = Execute("stop");
        Execute("stop");
        Assert.Single(Actions(First), action => action.Kind == "stop");
        Assert.True(Adapter(First).Mission.EndMissionCalled);
        Assert.Equal("applied", Receipt(First, stop));
    }

    [Theory]
    [InlineData("casualty")]
    [InlineData("result")]
    public void CampaignWrites_ReachRealServerGuards_AndHoldBothClients(string kind)
    {
        Start();
        First.Call(() =>
        {
            IMessage message = kind == "casualty"
                ? new NetworkRequestBattleCasualty("not-a-campaign-party", "not-a-campaign-troop", false, Manifest.InstanceId)
                : new NetworkBattleResultReady(Manifest.InstanceId, BattleState.AttackerVictory, 1);
            First.Resolve<INetwork>().SendAll(message);
        });
        Assert.Null(Server.Resolve<INavalLabSessionStore>().CampaignWriteBlocker);
        PumpAll();
        Assert.Equal(kind, Server.Resolve<INavalLabSessionStore>().CampaignWriteBlocker);
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
        PumpAll();
        foreach (var client in Clients)
        {
            Assert.Single(Actions(client), action => action.Kind == "hold");
            Assert.True(Adapter(client).TerminalHold);
            Assert.False(Adapter(client).Authority);
        }
        Assert.Empty(Server.InternalMessages.GetMessages<MapEventFinalized>());
        Assert.False(Server.Resolve<IBattleCompletionTracker>().TryConcludeAbandoned(
            Manifest.InstanceId, out _, out _, out _));
    }

    [Fact]
    public void DiskSave_IsDeniedByInstalledGamePatch_WithoutCallingSaveDriver()
    {
        Start();
        var driver = new Mock<ISaveDriver>(MockBehavior.Strict);
        int callbacks = 0;
        Server.Call(() => Game.Current.Save(new MetaData(), "naval-e2e-must-not-write", driver.Object, result =>
        {
            Assert.Equal(SaveResult.GeneralFailure, result);
            callbacks++;
        }));
        Assert.Equal(1, callbacks);
        driver.VerifyNoOtherCalls();
        Assert.Empty(Server.InternalMessages.GetMessages<GameSaved>());
    }
}
#endif
