using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Library;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

/// <summary>
/// The server owns kingdom decision resolution, so the patches never hand a decision screen back to
/// native resolution, and the vote manager only closes screens whose decision the server already took.
/// </summary>
public class KingdomDecisionScreenLifecycleTests
{
    private const string KingdomId = "kingdom-1";
    private const string OtherKingdomId = "kingdom-2";

    private static readonly FieldInfo ElectionDecisionField = typeof(KingdomElection).GetField(
        "_decision",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [Fact]
    public void ExecuteFinalSelection_EligiblePlayerVotes_LeavesResolutionToTheServer()
    {
        var decisionItem = ObjectHelper.SkipConstructor<SettlementDecisionItemVM>();
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.IsLocalPlayerEligible(decisionItem)).Returns(true);
        voteManager.Setup(manager => manager.TryPublishFinalVote(decisionItem)).Returns(true);

        bool runOriginal = RunWithVoteManager(
            voteManager.Object,
            () => DecisionItemBaseVMPatches.ExecuteFinalSelectionPrefix(decisionItem));

        Assert.False(runOriginal);
        voteManager.Verify(manager => manager.TryPublishFinalVote(decisionItem), Times.Once);
        voteManager.Verify(manager => manager.CloseDecisionItem(It.IsAny<DecisionItemBaseVM>()), Times.Never);
    }

    [Fact]
    public void ExecuteFinalSelection_VoteCannotBeRouted_ClosesTheScreenInsteadOfResolvingLocally()
    {
        var decisionItem = ObjectHelper.SkipConstructor<SettlementDecisionItemVM>();
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.IsLocalPlayerEligible(decisionItem)).Returns(true);
        voteManager.Setup(manager => manager.TryPublishFinalVote(decisionItem)).Returns(false);

        bool runOriginal = RunWithVoteManager(
            voteManager.Object,
            () => DecisionItemBaseVMPatches.ExecuteFinalSelectionPrefix(decisionItem));

        Assert.False(runOriginal);
        voteManager.Verify(manager => manager.TryPublishFinalVote(decisionItem), Times.Once);
        voteManager.Verify(manager => manager.CloseDecisionItem(decisionItem), Times.Once);
    }

    [Fact]
    public void ExecuteFinalSelection_IneligiblePlayer_ClosesTheScreenWithoutVoting()
    {
        var decisionItem = ObjectHelper.SkipConstructor<SettlementDecisionItemVM>();
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.IsLocalPlayerEligible(decisionItem)).Returns(false);

        bool runOriginal = RunWithVoteManager(
            voteManager.Object,
            () => DecisionItemBaseVMPatches.ExecuteFinalSelectionPrefix(decisionItem));

        Assert.False(runOriginal);
        voteManager.Verify(manager => manager.TryPublishFinalVote(It.IsAny<DecisionItemBaseVM>()), Times.Never);
        voteManager.Verify(manager => manager.CloseDecisionItem(decisionItem), Times.Once);
    }

    [Fact]
    public void RefreshWith_SuppressedDecision_DoesNotOpenAScreen()
    {
        var decisionsVm = ObjectHelper.SkipConstructor<KingdomDecisionsVM>();
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.ShouldSuppressLocalDecision(decision)).Returns(true);

        bool runOriginal = RunWithVoteManager(
            voteManager.Object,
            () => KingdomDecisionsVMPatches.RefreshWithPrefix(decisionsVm, decision));

        Assert.False(runOriginal);
        Assert.Null(decisionsVm.CurrentDecision);
    }

    [Fact]
    public void RefreshWith_UnsuppressedDecision_RunsNativeRefresh()
    {
        var decisionsVm = ObjectHelper.SkipConstructor<KingdomDecisionsVM>();
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.ShouldSuppressLocalDecision(decision)).Returns(false);

        bool runOriginal = RunWithVoteManager(
            voteManager.Object,
            () => KingdomDecisionsVMPatches.RefreshWithPrefix(decisionsVm, decision));

        Assert.True(runOriginal);
    }

    [Fact]
    public void IsOrphanedSubmittedDecisionItem_SecondSubmittedScreenInTheKingdom_SurvivesWhileItsDecisionStands()
    {
        Kingdom kingdom = CreateKingdom();
        CampaignKingdomDecision liveDecision = CreateDecision(kingdom);
        CampaignKingdomDecision takenDecision = CreateDecision(kingdom);
        kingdom._unresolvedDecisions.Add(liveDecision);

        using var voteManager = CreateVoteManager(CreateObjectManager(kingdom));

        Assert.True(voteManager.IsOrphanedSubmittedDecisionItem(CreateDecisionItem(takenDecision, true), KingdomId));
        Assert.False(voteManager.IsOrphanedSubmittedDecisionItem(CreateDecisionItem(liveDecision, true), KingdomId));
    }

    [Fact]
    public void IsOrphanedSubmittedDecisionItem_ScreenStillBeingVotedOn_IsLeftOpen()
    {
        Kingdom kingdom = CreateKingdom();
        CampaignKingdomDecision takenDecision = CreateDecision(kingdom);

        using var voteManager = CreateVoteManager(CreateObjectManager(kingdom));

        Assert.False(voteManager.IsOrphanedSubmittedDecisionItem(CreateDecisionItem(takenDecision, false), KingdomId));
    }

    [Fact]
    public void IsOrphanedSubmittedDecisionItem_ScreenForAnotherKingdom_IsLeftOpen()
    {
        Kingdom kingdom = CreateKingdom();
        CampaignKingdomDecision takenDecision = CreateDecision(kingdom);

        using var voteManager = CreateVoteManager(CreateObjectManager(kingdom));

        Assert.False(voteManager.IsOrphanedSubmittedDecisionItem(CreateDecisionItem(takenDecision, true), OtherKingdomId));
    }

    [Fact]
    public void IsLocalPlayerEligible_ScreenWithoutAnElection_IsNotEligible()
    {
        using var voteManager = CreateVoteManager(new Mock<IObjectManager>().Object);

        Assert.False(voteManager.IsLocalPlayerEligible(null!));
        Assert.False(voteManager.IsLocalPlayerEligible(ObjectHelper.SkipConstructor<SettlementDecisionItemVM>()));
    }

    [Fact]
    public void IsLocalPlayerEligible_ElectionWithoutADecision_IsNotEligible()
    {
        using var voteManager = CreateVoteManager(new Mock<IObjectManager>().Object);
        var decisionItem = ObjectHelper.SkipConstructor<SettlementDecisionItemVM>();
        decisionItem.KingdomDecisionMaker = ObjectHelper.SkipConstructor<KingdomElection>();

        Assert.False(voteManager.IsLocalPlayerEligible(decisionItem));
    }

    private static Kingdom CreateKingdom()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision>();
        return kingdom;
    }

    private static CampaignKingdomDecision CreateDecision(Kingdom kingdom)
    {
        var decision = ObjectHelper.SkipConstructor<SettlementClaimantDecision>();
        decision._kingdom = kingdom;
        return decision;
    }

    private static DecisionItemBaseVM CreateDecisionItem(CampaignKingdomDecision decision, bool finalSelectionDone)
    {
        var election = ObjectHelper.SkipConstructor<KingdomElection>();
        // the election constructor needs a live campaign and _decision is readonly, so seed the field
        ElectionDecisionField.SetValue(election, decision);

        var decisionItem = ObjectHelper.SkipConstructor<SettlementDecisionItemVM>();
        decisionItem.KingdomDecisionMaker = election;
        decisionItem._finalSelectionDone = finalSelectionDone;
        return decisionItem;
    }

    private static IObjectManager CreateObjectManager(Kingdom kingdom)
    {
        var objectManager = new Mock<IObjectManager>();
        string kingdomId = KingdomId;
        objectManager.Setup(manager => manager.TryGetId(kingdom, out kingdomId)).Returns(true);
        return objectManager.Object;
    }

    private static KingdomDecisionVoteManager CreateVoteManager(IObjectManager objectManager)
    {
        return new KingdomDecisionVoteManager(
            new Mock<IPlayerManager>().Object,
            objectManager,
            new Mock<IMessageBroker>().Object,
            new Mock<IKingdomDecisionOutcomeResolver>().Object,
            new Mock<IKingdomDecisionOutcomeOrder>().Object,
            new Mock<IKingdomDecisionRoundPresentation>().Object);
    }

    private static bool RunWithVoteManager(IKingdomDecisionVoteManager voteManager, Func<bool> act)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(voteManager).As<IKingdomDecisionVoteManager>();
        using var container = builder.Build();

        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                return act();
            }
        }
        finally
        {
            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }
}
