using Autofac;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Moq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

public class GangLeaderNeedsToOffloadStolenGoodsGateBypassTests : IDisposable
{
    private static readonly FieldInfo QuestGiverField = AccessTools.Field(typeof(QuestBase), "_questGiver");

    public void Dispose()
    {
        ContainerProvider.Clear();
    }

    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

    private static Quest NewQuestFor(Hero giver)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        QuestGiverField.SetValue(quest, giver);
        return quest;
    }

    private static void SetUpNonOwningPeer(Hero giver, string recordedOwnerControllerId, string localControllerId)
    {
        var registry = new IssueOwnershipRegistry();
        registry.SetOwner(giver, recordedOwnerControllerId);

        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns(localControllerId);

        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(p => p.AllowOriginal()).Returns(false);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(controllerIdProvider.Object).As<IControllerIdProvider>();
        builder.RegisterInstance((IIssueOwnershipRegistry)registry).As<IIssueOwnershipRegistry>();
        builder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());
    }

    [Fact]
    public void BlockAndReportTerminalOutcome_AnOpenAllowedThreadNeverOverridesAResolvedNonOwner()
    {
        var giver = NewHero();
        SetUpNonOwningPeer(giver, "player-A", "player-B");
        var quest = NewQuestFor(giver);

        bool result;
        using (new AllowedThread())
        {
            result = GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(quest, IssueFinalizeReason.QuestFail);
        }

        Assert.False(result);
    }
}
