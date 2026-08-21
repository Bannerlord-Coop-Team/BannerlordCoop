using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.SiegeEvents;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(ModInformationRoleCollection.Name)]
public class AiSiegeTerminalPolicyTests
{
    private static AiSiegeTerminalPolicy CreatePolicy()
    {
        return new AiSiegeTerminalPolicy(
            new Mock<IAiSiegeAssaultReadiness>().Object,
            new Mock<ILogger>().Object,
            new Mock<IMessageBroker>().Object);
    }

    [Fact]
    public void StarvingPreparedViableAiSiege_Assaults()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(isAssaultViable: true));

        Assert.Equal(AiSiegeTerminalDecision.Assault, decision);
    }

    [Fact]
    public void StarvingPreparedInviableAiSiege_Withdraws()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(isAssaultViable: false));

        Assert.Equal(AiSiegeTerminalDecision.Withdraw, decision);
    }

    [Fact]
    public void ActiveTransition_DefersTerminalAction()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            hasActiveTransition: true));

        Assert.Equal(AiSiegeTerminalDecision.Defer, decision);
    }

    [Fact]
    public void DeferredTransition_IsRetriedOnce()
    {
        var policy = CreatePolicy();
        var state = new AiSiegeTerminalTransitionState(
            ObjectHelper.SkipConstructor<MobileParty>(),
            ObjectHelper.SkipConstructor<SiegeEvent>());
        var retried = new List<AiSiegeTerminalTransitionState>();
        policy.Defer(state);
        policy.Defer(state);

        policy.RetryDeferredTransitions(retried.Add);
        policy.RetryDeferredTransitions(retried.Add);

        var retry = Assert.Single(retried);
        Assert.Same(state.LeaderParty, retry.LeaderParty);
        Assert.Same(state.SiegeEvent, retry.SiegeEvent);
    }

    [Fact]
    public void DeferredTransition_SurvivesSaveAndReload()
    {
        var records = new Dictionary<string, object>();
        var state = new AiSiegeTerminalTransitionState(
            ObjectHelper.SkipConstructor<MobileParty>(),
            ObjectHelper.SkipConstructor<SiegeEvent>());
        bool previousRole = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var original = CreatePolicy();
            original.Defer(state);
            original.SyncData(new TestDataStore(isSaving: true, records));

            var restored = CreatePolicy();
            restored.SyncData(new TestDataStore(isSaving: false, records));
            var retried = new List<AiSiegeTerminalTransitionState>();
            restored.RetryDeferredTransitions(retried.Add);

            var retry = Assert.Single(retried);
            Assert.Same(state.LeaderParty, retry.LeaderParty);
            Assert.Same(state.SiegeEvent, retry.SiegeEvent);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Fact]
    public void MapEventFinalized_QueuesRetryUntilAfterEncounterClose()
    {
        using var messageBroker = new MessageBroker();
        var scheduled = new Queue<Action>();
        var timeline = new List<string>();
        var state = new AiSiegeTerminalTransitionState(
            ObjectHelper.SkipConstructor<MobileParty>(),
            ObjectHelper.SkipConstructor<SiegeEvent>());
        bool previousRole = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            using var policy = new AiSiegeTerminalPolicy(
                new Mock<IAiSiegeAssaultReadiness>().Object,
                new Mock<ILogger>().Object,
                messageBroker,
                action =>
                {
                    timeline.Add("retry queued");
                    scheduled.Enqueue(action);
                },
                _ => timeline.Add("retry"));
            policy.Defer(state);

            timeline.Add("finalize started");
            messageBroker.Publish(this, new MapEventFinalized(null));
            timeline.Add("old event closed");

            Assert.Equal(
                new[] { "finalize started", "retry queued", "old event closed" },
                timeline);

            Assert.Single(scheduled).Invoke();

            Assert.Equal("retry", timeline[3]);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Fact]
    public void PlayerLedSiege_IsUnaffected()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            isPlayerLed: true));

        Assert.Equal(AiSiegeTerminalDecision.None, decision);
    }

    [Fact]
    public void EndedSiegeState_IsCleanAcrossPolicyInstances()
    {
        var context = CreateContext(isAssaultViable: true, isCurrentSiege: false);

        Assert.Equal(AiSiegeTerminalDecision.None, CreatePolicy().GetDecision(context));
        Assert.Equal(AiSiegeTerminalDecision.None, CreatePolicy().GetDecision(context));
    }

    [Fact]
    public void StarvingUnpreparedSiege_Withdraws()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            isPrepared: false));

        Assert.Equal(AiSiegeTerminalDecision.Withdraw, decision);
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data;
                return true;
            }

            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }

    private static AiSiegeTerminalContext CreateContext(
        bool isAssaultViable,
        bool isPrepared = true,
        bool isPlayerLed = false,
        bool isCurrentSiege = true,
        bool hasActiveTransition = false)
    {
        return new AiSiegeTerminalContext(
            isFoodProblem: true,
            isPrepared,
            isPlayerLed,
            isCurrentSiege,
            hasActiveTransition,
            isAssaultViable);
    }
}
