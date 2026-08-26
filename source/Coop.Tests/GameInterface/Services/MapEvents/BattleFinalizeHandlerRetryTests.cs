using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using HarmonyLib;
using Moq;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

public class BattleFinalizeHandlerRetryTests : IDisposable
{
    private readonly IMessageBroker broker = new MessageBroker();
    private readonly ThrowingSiegeMapEventLeaderReconciler leaderReconciler = new();
    private readonly BattleFinalizeHandler handler;

    public BattleFinalizeHandlerRetryTests()
    {
        handler = new BattleFinalizeHandler(
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<IMobilePartyBehaviorSnapshot>(),
            Mock.Of<INetwork>(),
            null,
            Mock.Of<IBattleTroopReserveBuilder>(),
            Mock.Of<ISettlementInterface>(),
            Mock.Of<IBattleHostRegistry>(),
            Mock.Of<IPlayerManager>(),
            leaderReconciler);
    }

    [Fact]
    public void FailedFinalize_ReleasesMarkerSoNextAttemptCanRetry()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var finalize = AccessTools.Method(typeof(BattleFinalizeHandler), "FinalizeAndCollectPlayers");

        finalize.Invoke(handler, new object[] { mapEvent, null });
        finalize.Invoke(handler, new object[] { mapEvent, null });

        Assert.Equal(2, leaderReconciler.RestoreBeforeFinalizeCalls);
    }

    private sealed class ThrowingSiegeMapEventLeaderReconciler : ISiegeMapEventLeaderReconciler
    {
        public int RestoreBeforeFinalizeCalls { get; private set; }

        public bool RestoreAfterJoin(MapEvent mapEvent, PartyBase joinedParty) => false;

        public bool RestoreBeforeFinalize(
            MapEvent mapEvent,
            out PartyBase replacedLeader,
            out PartyBase restoredLeader)
        {
            RestoreBeforeFinalizeCalls++;
            replacedLeader = null;
            restoredLeader = null;
            throw new InvalidOperationException("finalize setup failed");
        }
    }

    public void Dispose()
    {
        handler.Dispose();
        broker.Dispose();
    }
}
