using System;
using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Patches;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class ArmyNeedsSuppliesOwnershipTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly QuestOwner service = new();

    public ArmyNeedsSuppliesOwnershipTests()
    {
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object);
        builder.RegisterInstance(service).As<IArmyNeedsSuppliesQuest>();
        ContainerProvider.SetContainer(builder.Build());
        ModInformation.IsServer = false;
    }

    private sealed class QuestOwner : IArmyNeedsSuppliesQuest
    {
        public Quest LocalQuest { get; set; }
        public bool IsLocalOwner(Quest quest) => quest == LocalQuest;
        public bool IsOwner(Quest quest, Hero hero) => false;
        public bool TryOpenOwnerScope(Quest quest, out IDisposable scope) { scope = null; return false; }
        public void ReplayQuestAccepted(Hero owner) { }
        public bool TryCaptureQuestFields(Hero owner, out ArmyNeedsSuppliesAcceptance fields) { fields = null; return false; }
        public void MirrorQuestAccepted(Hero owner, ArmyNeedsSuppliesAcceptance fields) { }
        public void RejectAcceptance(Hero owner) { }
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        ModInformation.IsServer = wasServer;
        ArmyNeedsSuppliesCharacterChangePatch.OldPlayer = null;
        ArmyNeedsSuppliesCharacterChangePatch.NewPlayer = null;
        ArmyNeedsSuppliesJournalViewPatch.IsBuilding = false;
    }

    [Fact]
    public void ObserverCannotOfferDeliveryEvenUnderAnAllowedThread()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var result = true;
        using (new AllowedThread())
            Assert.False(ArmyNeedsSuppliesDeliveryConditionPatches.Prefix(quest, ref result));
        Assert.False(result);
    }

    [Fact]
    public void AutonomousClientTimeoutCannotFinalizeQuest()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        using (new AllowedThread())
            Assert.False(ArmyNeedsSuppliesTerminalPatches.Prefix(quest, out _));
    }

    [Fact]
    public void ReceivedOwnerCompletionCanFinalizeQuest()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        service.LocalQuest = quest;
        using (new AllowedThread())
        using (new IssueFinalizeAuthorityGuard())
            Assert.True(ArmyNeedsSuppliesTerminalPatches.Prefix(quest, out _));
    }

    [Fact]
    public void CharacterChangeDoesNotCancelAnotherPlayersQuest()
    {
        ModInformation.IsServer = true;
        ArmyNeedsSuppliesCharacterChangePatch.OldPlayer = ObjectHelper.SkipConstructor<Hero>();
        ArmyNeedsSuppliesCharacterChangePatch.NewPlayer = ObjectHelper.SkipConstructor<Hero>();
        Assert.False(ArmyNeedsSuppliesTerminalPatches.Prefix(ObjectHelper.SkipConstructor<Quest>(), out _));
    }

    [Fact]
    public void PersonalQuestListHidesOtherPlayersSupplyQuest()
    {
        var own = ObjectHelper.SkipConstructor<Quest>();
        var other = ObjectHelper.SkipConstructor<Quest>();
        service.LocalQuest = own;
        MBReadOnlyList<QuestBase> quests = new MBList<QuestBase> { own, other };

        ArmyNeedsSuppliesJournalViewPatch.Prefix(out var previous);
        try { ArmyNeedsSuppliesPersonalJournalPatch.Postfix(ref quests); }
        finally { ArmyNeedsSuppliesJournalViewPatch.Finalizer(previous); }

        Assert.Same(own, Assert.Single(quests));
    }

    [Fact]
    public void EngineQuestCollectionRetainsItsIdentityAndNullIndexesOutsideJournalView()
    {
        MBReadOnlyList<QuestBase> quests = new MBList<QuestBase> { ObjectHelper.SkipConstructor<Quest>(), null };
        var original = quests;

        ArmyNeedsSuppliesPersonalJournalPatch.Postfix(ref quests);

        Assert.Same(original, quests);
        Assert.Equal(2, quests.Count);
        Assert.Null(quests[1]);
    }
}
