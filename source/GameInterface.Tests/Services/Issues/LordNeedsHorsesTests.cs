using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Data;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.LordNeedsHorses;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Patches;
using GameInterface.Services.Players;
using GameInterface.Surrogates;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class LordNeedsHorsesTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly IContainer container;
    private readonly Mock<ILordNeedsHorsesQuest> quests = new();

    public LordNeedsHorsesTests()
    {
        var builder = new ContainerBuilder();
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        builder.RegisterInstance(policy.Object);
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-A");
        builder.RegisterInstance(controller.Object);
        builder.RegisterInstance(quests.Object);
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        container.Dispose();
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void PendingAcceptance_StopsOnlyItsConversationAndClearsWhenItEnds()
    {
        var waiting = ObjectHelper.SkipConstructor<ConversationManager>();
        var other = ObjectHelper.SkipConstructor<ConversationManager>();
        LordNeedsHorsesQuestType.PendingAcceptances.Add(waiting, ObjectHelper.SkipConstructor<Hero>());
        Assert.False(LordNeedsHorsesAcceptanceConversationPatch.Prefix(waiting));
        Assert.True(LordNeedsHorsesAcceptanceConversationPatch.Prefix(other));
        LordNeedsHorsesConversationEndPatch.Postfix(waiting);
        Assert.True(LordNeedsHorsesAcceptanceConversationPatch.Prefix(waiting));
    }

    [Fact]
    public void Acceptance_PreservesTheSynchronizedOfferDifficultyAndLeavesOtherIssueTypesUnchanged()
    {
        var horse = ObjectHelper.SkipConstructor<Issue>();
        var other = ObjectHelper.SkipConstructor<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>();
        var difficulty = AccessTools.Field(typeof(IssueBase), "_issueDifficultyMultiplier");
        difficulty.SetValue(horse, 0.4f);
        difficulty.SetValue(other, 0.4f);
        LordNeedsHorsesDifficultyPatch.SetAcceptedDifficulty(horse, 0.9f);
        LordNeedsHorsesDifficultyPatch.SetAcceptedDifficulty(other, 0.9f);
        Assert.Equal(0.4f, difficulty.GetValue(horse));
        Assert.Equal(0.9f, difficulty.GetValue(other));

        foreach (var name in new[] { nameof(IssueBase.StartIssueWithQuest), nameof(IssueBase.StartIssueWithAlternativeSolution) })
        {
            var instructions = LordNeedsHorsesDifficultyPatch.Transpiler(
                PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(IssueBase), name))).ToArray();
            Assert.DoesNotContain(instructions, instruction => instruction.StoresField(difficulty));
            Assert.Single(instructions.Where(instruction => instruction.Calls(
                AccessTools.Method(typeof(LordNeedsHorsesDifficultyPatch), nameof(LordNeedsHorsesDifficultyPatch.SetAcceptedDifficulty)))));
        }
    }

    [Fact]
    public void CreationWire_RetainsTheOfferedDifficultyAndMountRequirements()
    {
        _ = new SurrogateCollection();
        var offer = new NetworkLordNeedsHorsesIssueCreated("giver", "horse", 6, 140, 8,
            new CampaignTime(2345), "horse_issue", 17, 0.4f);
        var received = GenericAcceptFieldsSerializer.Deserialize<NetworkLordNeedsHorsesIssueCreated>(GenericAcceptFieldsSerializer.Serialize(offer));
        Assert.Equal(offer.DifficultyMultiplier, received.DifficultyMultiplier);
        Assert.Equal(offer.NumMountsToBeDelivered, received.NumMountsToBeDelivered);
        Assert.Equal(offer.MountValuePerUnit, received.MountValuePerUnit);
        Assert.Equal(offer.Generation, received.Generation);
        Assert.Equal(offer.DueTime, received.DueTime);
    }

    [Fact]
    public void CompanionSelection_UsesDetachedRostersAndRetainsCountsWoundsAndXp()
    {
        ModInformation.IsServer = false;
        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        var partyRoster = new TroopRoster();
        partyRoster.AddToCounts(troop, 10, false, 2, 700);
        var pending = new TroopRoster();
        pending.AddToCounts(troop, 2, false, 0, 140);
        var issue = ObjectHelper.SkipConstructor<Issue>();
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops)).SetValue(issue, pending);
        var screen = ObjectHelper.SkipConstructor<PartyScreenLogic>();
        var data = new PartyScreenLogicInitializationData
        {
            LeftMemberRoster = pending,
            RightMemberRoster = partyRoster,
            RightPrisonerRoster = new TroopRoster(),
        };
        var service = new LordNeedsHorsesQuest(Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(),
            Mock.Of<IIssueOwnershipRegistry>(), Mock.Of<IControllerIdProvider>(), Mock.Of<IMessageBroker>(),
            Mock.Of<IBinaryPackageFactory>(), Mock.Of<IIssueGenerationRegistry>());

        service.PrepareTroopSelection(issue, screen, ref data);
        data.RightMemberRoster.AddToCounts(troop, -3, false, 0, -210);
        data.LeftMemberRoster.AddToCounts(troop, 3, false, 0, 210);

        Assert.NotSame(partyRoster, data.RightMemberRoster);
        Assert.NotSame(pending, data.LeftMemberRoster);
        Assert.Equal(10, partyRoster.GetTroopCount(troop));
        Assert.Equal(2, partyRoster.TotalWoundedRegulars);
        Assert.Equal(700, partyRoster.GetElementXp(troop));
        Assert.Equal(2, pending.GetTroopCount(troop));
        Assert.Equal(140, pending.GetElementXp(troop));
        Assert.Equal(350, data.RightMemberRoster.GetElementXp(troop));
        Assert.Equal(350, data.LeftMemberRoster.GetElementXp(troop));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompanionConfirmation_DoesNotEnterGenericPartyCommit(bool accepted)
    {
        var screen = ObjectHelper.SkipConstructor<PartyScreenLogic>();
        quests.Setup(x => x.TryCompleteTroopSelection(screen, out accepted)).Returns(true);
        bool result = !accepted;
        Assert.False(PartyScreenLogicPatches.DoneLogicPrefix(screen, ref result, false));
        Assert.Equal(accepted, result);
        quests.Verify(x => x.TryCompleteTroopSelection(screen, out accepted), Times.Once);
    }

    [Fact]
    public void SavedQuestLoad_DoesNotReadAnyPlayersInventoryBeforeRegistration()
    {
        var method = AccessTools.DeclaredMethod(typeof(Quest), "InitializeQuestOnGameLoad");
        var instructions = LordNeedsHorsesLoadPatch.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToArray();
        Assert.DoesNotContain(instructions, x => x.Calls(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty))));
        Assert.DoesNotContain(instructions, x => x.Calls(AccessTools.DeclaredMethod(typeof(Quest), "GetNumQuestMountsInInventory")));
        Assert.Single(instructions.Where(x => x.Calls(AccessTools.Method(typeof(LordNeedsHorsesLoadPatch), "SavedMountCount"))));
        Assert.Contains(instructions, x => x.opcode == OpCodes.Ldc_I4_M1);
        Assert.Contains(instructions, x => x.Calls(AccessTools.DeclaredMethod(typeof(Quest), "SetDialogs")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SavedQuestLoad_RetainsJournalProgressWithOrWithoutSavedLogReference(bool hasSavedReference)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var log = new JournalLog(new CampaignTime(12345), TextObject.Empty, TextObject.Empty, 3, 7, LogType.Discreate);
        AccessTools.Field(typeof(QuestBase), "_journalEntries").SetValue(quest, new MBList<JournalLog> { log });
        AccessTools.Field(typeof(Quest), "_numMountsToBeDelivered").SetValue(quest, 7);
        AccessTools.Field(typeof(Quest), "_questJournalEntry").SetValue(quest, hasSavedReference ? log : null);
        Assert.Equal(3, LordNeedsHorsesLoadPatch.SavedMountCount(quest));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Notifications_UsePersistentJournalOwnershipForDirectAndCompanionQuests(bool localOwner)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var issue = ObjectHelper.SkipConstructor<Issue>();
        quests.Setup(x => x.IsLocalJournalOwner(quest, null)).Returns(localOwner);
        quests.Setup(x => x.IsLocalJournalOwner(issue, null)).Returns(localOwner);
        Assert.Equal(localOwner, LordNeedsHorsesNotificationPatch.QuestNotification(quest));
        Assert.Equal(localOwner, LordNeedsHorsesIssueNotificationPatch.IssueNotification(issue));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CompanionFunding_RequiresServerStartAuthorityEvenOnAllowedThread(bool server, bool starting, bool expected)
    {
        ModInformation.IsServer = server;
        using var allowed = new AllowedThread();
        using var start = starting ? new AlternativeSolutionStartAuthorityGuard() : null;
        Assert.Equal(expected, (bool)AccessTools.Method(typeof(LordNeedsHorsesFundingPatch), "Prefix").Invoke(null, null));
    }

    [Fact]
    public void TerminalMirror_DoesNotReapplyRewardsOnClient()
    {
        ModInformation.IsServer = false;
        using var allowed = new AllowedThread();
        using var finalization = new IssueFinalizeAuthorityGuard();
        Assert.False((bool)AccessTools.Method(typeof(LordNeedsHorsesConsequencesPatch), "Consequences").Invoke(null, null));
    }

    [Fact]
    public void QuestScreen_FiltersAllThreeInstalledSourcesBeforeCreatingItems()
    {
        var constructor = AccessTools.Constructor(typeof(QuestsVM), new[] { typeof(Action) });
        var instructions = LordNeedsHorsesJournalVisibilityPatch.Transpiler(PatchProcessor.GetOriginalInstructions(constructor)).ToArray();
        var filters = instructions.Select(x => x.operand).OfType<MethodInfo>()
            .Where(x => x.DeclaringType == typeof(LordNeedsHorsesJournalVisibilityPatch)).Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "VisibleQuests", "VisibleIssues", "VisibleHistory" }, filters);
    }

    [Fact]
    public void CompletedHistory_ShowsOnlyLocalPlayersHorseQuestsAndPreservesOtherTypes()
    {
        ModInformation.IsServer = false;
        var mine = ObjectHelper.SkipConstructor<LordNeedsHorsesJournalLogEntry>();
        var other = ObjectHelper.SkipConstructor<LordNeedsHorsesJournalLogEntry>();
        AccessTools.Field(typeof(LordNeedsHorsesJournalLogEntry), nameof(LordNeedsHorsesJournalLogEntry.OwnerControllerId)).SetValue(mine, "player-A");
        AccessTools.Field(typeof(LordNeedsHorsesJournalLogEntry), nameof(LordNeedsHorsesJournalLogEntry.OwnerControllerId)).SetValue(other, "player-B");
        var unrelated = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var entries = new JournalLogEntry[] { mine, other, unrelated };
        var visible = (IEnumerable<JournalLogEntry>)AccessTools.Method(typeof(LordNeedsHorsesJournalVisibilityPatch), "VisibleHistory")
            .Invoke(null, new object[] { entries });
        Assert.Equal(new JournalLogEntry[] { mine, unrelated }, visible);
        Assert.Equal(3, entries.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrackingView_FiltersOtherPlayersQuestWithoutChangingTheRegistryOrReceivedCleanup(bool applyingMessage)
    {
        ModInformation.IsServer = false;
        var myGiver = ObjectHelper.SkipConstructor<Hero>();
        var otherGiver = ObjectHelper.SkipConstructor<Hero>();
        var mine = ObjectHelper.SkipConstructor<Quest>();
        var other = ObjectHelper.SkipConstructor<Quest>();
        AccessTools.Property(typeof(QuestBase), nameof(QuestBase.QuestGiver)).SetValue(mine, myGiver);
        AccessTools.Property(typeof(QuestBase), nameof(QuestBase.QuestGiver)).SetValue(other, otherGiver);
        quests.Setup(x => x.IsLocalOwner(myGiver)).Returns(true);
        var source = new Dictionary<ITrackableCampaignObject, List<QuestBase>>
        {
            [myGiver] = new List<QuestBase> { mine },
            [otherGiver] = new List<QuestBase> { other },
        };
        var view = source.GetReadOnlyDictionary();
        using var received = applyingMessage ? new AllowedThread() : null;
        LordNeedsHorsesTrackedQuestVisibilityPatch.Postfix(ref view);
        Assert.True(view.ContainsKey(myGiver));
        Assert.Equal(applyingMessage, view.ContainsKey(otherGiver));
        Assert.Equal(2, source.Count);
        Assert.Same(other, Assert.Single(source[otherGiver]));
    }

    [Fact]
    public void JournalWire_RetainsGenerationProgressTextTimeAndOutcome()
    {
        _ = new SurrogateCollection();
        var entry = new LordNeedsHorsesJournalEntry(new CampaignTime(12345), new byte[] { 1, 2, 3 },
            new byte[] { 4, 5 }, 3, 7, 1);
        var message = new NetworkLordNeedsHorsesJournal("giver", 8, new[] { entry }, 3, true, 4, 0.8f, true);
        var result = GenericAcceptFieldsSerializer.Deserialize<NetworkLordNeedsHorsesJournal>(GenericAcceptFieldsSerializer.Serialize(message));
        Assert.Equal(message.OwnerId, result.OwnerId);
        Assert.Equal(8, result.Generation);
        Assert.Equal(3, result.Progress);
        Assert.True(result.IsQuest);
        Assert.Equal(4, result.IssueStatus);
        Assert.Equal(0.8f, result.DifficultyMultiplier);
        Assert.True(result.EffectsResolved);
        var retained = Assert.Single(result.Entries);
        Assert.Equal(entry.Time, retained.Time);
        Assert.Equal(entry.Text, retained.Text);
        Assert.Equal(entry.Task, retained.Task);
        Assert.Equal(3, retained.Progress);
        Assert.Equal(7, retained.Range);
        Assert.Equal(1, retained.Type);
    }

    [Fact]
    public void AcceptanceWire_RetainsPlayerIdentityDeadlineAndInitialServerJournal()
    {
        _ = new SurrogateCollection();
        var log = new LordNeedsHorsesJournalEntry(new CampaignTime(12345), new byte[] { 1 },
            new byte[] { 2 }, 0, 7, (int)LogType.Discreate);
        var fields = new LordNeedsHorsesAcceptFields("hero-A", "party-A", new CampaignTime(99999),
            new NetworkLordNeedsHorsesJournal("giver", 8, new[] { log }, 0, true, 0, 0.8f, false));
        var result = GenericAcceptFieldsSerializer.Deserialize<LordNeedsHorsesAcceptFields>(
            GenericAcceptFieldsSerializer.Serialize(fields));
        Assert.Equal(fields.PlayerHeroId, result.PlayerHeroId);
        Assert.Equal(fields.PlayerPartyId, result.PlayerPartyId);
        Assert.Equal(fields.DueTime, result.DueTime);
        Assert.Equal("giver", result.Journal.OwnerId);
        Assert.Equal(8, result.Journal.Generation);
        Assert.Equal(0.8f, result.Journal.DifficultyMultiplier);
        Assert.Equal(log.Time, Assert.Single(result.Journal.Entries).Time);
    }
}
