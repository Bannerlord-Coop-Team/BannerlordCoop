using Autofac;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Visibility;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class IssueFrameworkIsolationTests
{
    [Fact]
    public void Bootstrap_LeavesStolenGoodsAcceptanceAndFinalizationToFramework()
    {
        QuestTypeBootstrap.EnsureAllMigratedTypesRegistered();
        var issue = ObjectHelper.SkipConstructor<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>();

        Assert.Null(QuestTypeRegistry.Get(issue));
        Assert.True(IssueFinalizedOwnershipGatePatch.Prefix(issue));
        Assert.True((bool)AccessTools.Method(typeof(IssueManagerAlternativeSolutionTroopsPatches),
            "TryToMakeTroopsReturnPrefix").Invoke(null, new object[] { issue }));
    }

    [Theory]
    [InlineData(true, "player-A", false)]
    [InlineData(true, "player-B", true)]
    [InlineData(false, "player-A", false)]
    [InlineData(false, "player-B", true)]
    public void CompanionJournal_UsesTheOwningFramework(bool horse, string ownerId, bool hidden)
    {
        QuestTypeBootstrap.EnsureAllMigratedTypesRegistered();
        var giver = ObjectHelper.SkipConstructor<Hero>();
        IssueBase issue = horse
            ? ObjectHelper.SkipConstructor<LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue>()
            : ObjectHelper.SkipConstructor<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>();
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IssueOwner)).SetValue(issue, giver);
        AccessTools.Field(typeof(IssueBase), "_issueState").SetValue(issue, IssueBase.IssueState.SolvingWithAlternativeSolution);

        var legacy = new Mock<IIssueOwnershipRegistry>();
        legacy.Setup(x => x.IsLocalPeerOwner(giver)).Returns(horse && ownerId == "player-A");
        var ownership = new Mock<global::GameInterface.Services.Issues.Framework.Interface.IIssueOwnershipRegistry>();
        var resolvedOwner = ownerId;
        ownership.Setup(x => x.TryGetOwner("giver", issue.StringId, out resolvedOwner)).Returns(!horse);
        var objects = new Mock<IObjectManager>();
        var giverId = "giver";
        objects.Setup(x => x.TryGetId(giver, out giverId)).Returns(true);
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-A");
        var builder = new ContainerBuilder();
        builder.RegisterInstance(legacy.Object);
        builder.RegisterInstance(ownership.Object);
        builder.RegisterInstance(objects.Object);
        builder.RegisterInstance(controller.Object);
        using var container = builder.Build();
        ContainerProvider.SetContainer(container);
        try
        {
            Assert.Equal(hidden, QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue));
            Assert.True(issue.IsSolvingWithAlternative);
        }
        finally
        {
            ContainerProvider.Clear();
        }
    }
}
