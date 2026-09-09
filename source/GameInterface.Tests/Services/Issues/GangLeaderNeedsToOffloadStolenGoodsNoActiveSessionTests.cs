using Autofac;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Patches;
using GameInterface.Tests;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using Quest = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

public class GangLeaderNeedsToOffloadStolenGoodsNoActiveSessionTests : IDisposable
{
    public GangLeaderNeedsToOffloadStolenGoodsNoActiveSessionTests()
    {
        _ = GangLeaderNeedsToOffloadStolenGoodsQuestType.AlternativeSolutionFreeze;
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
    }

    private static Issue NewIssue() => ObjectHelper.SkipConstructor<Issue>();
    private static Quest NewQuest() => ObjectHelper.SkipConstructor<Quest>();

    [Fact]
    public void QuestSolutionStartGate_WithNoActiveCoopSession_LetsTheRealAcceptRunInstead()
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());

        var acceptResult = false;
        var result = GenericQuestTypeQuestSolutionStartOwnershipGatePatch.Prefix(NewIssue(), ref acceptResult);

        Assert.True(result);
        Assert.False(acceptResult);
    }

    [Fact]
    public void AlternativeSolutionStartGate_WithNoActiveCoopSession_LetsTheRealStartRunInstead()
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());

        var result = GenericQuestTypeAlternativeSolutionStartOwnershipGatePatch.Prefix(NewIssue());

        Assert.True(result);
    }

    [Fact]
    public void GangLeaderOwnershipGate_WithNoActiveCoopSession_LetsTheRealSuccessMethodRunInstead()
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());

        var result = GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches.SucceedQuestByPayingAndKeepingTheGoodsPrefix(NewQuest());

        Assert.True(result);
    }

    [Fact]
    public void IssueFinalizedGate_WithNoActiveCoopSessionForAnAllowlistedType_LetsTheRealFinalizeRunInstead()
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());

        var result = IssueFinalizedOwnershipGatePatch.Prefix(NewIssue());

        Assert.True(result);
    }
}
