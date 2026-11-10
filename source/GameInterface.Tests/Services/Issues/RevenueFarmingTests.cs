using Common;
using Common.Network;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;

using GameInterface.Services.Issues.Quests.RevenueFarming;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Surrogates;
using Moq;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;
using Issue = RevenueFarmingIssueBehavior.RevenueFarmingIssue;

[Collection(ModInformationRoleCollection.Name)]
public class RevenueFarmingTests
{
    [Fact]
    public void OwnershipUsesRegisteredLocalPlayerDuringAnotherPlayersFinalization()
    {
        var localHero = ObjectHelper.SkipConstructor<Hero>();
        var otherHero = ObjectHelper.SkipConstructor<Hero>();
        var localQuest = ObjectHelper.SkipConstructor<Quest>();
        var otherQuest = ObjectHelper.SkipConstructor<Quest>();
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(value => value.ControllerId).Returns("local");
        var sessions = new RevenueFarmingSessionStore(controller.Object);
        sessions.Register(localQuest, localHero, "local");
        sessions.Register(otherQuest, otherHero, "other");

        using (new MainHeroSubstitutionScope(otherHero, null))
        {
            Assert.True(sessions.IsLocal(localQuest));
            Assert.False(sessions.IsLocal(otherQuest));
        }
        Assert.Throws<InvalidOperationException>(() => sessions.Register(localQuest, otherHero, "other"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavingOmitsEndedSessionsWithoutDroppingPendingNavigation(bool dismissed)
    {
        var sessions = new RevenueFarmingSessionStore(Mock.Of<IControllerIdProvider>());
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var ended = LinkedQuest("ended");
        var terminal = sessions.Register(ended, hero, "owner");
        if (dismissed) ended.QuestGiver.Issue = null;
        else ended._questState = QuestBase.QuestStates.Finalized;
        var ongoing = LinkedQuest("ongoing");
        var active = sessions.Register(ongoing, ObjectHelper.SkipConstructor<Hero>(), "other");
        var records = new Dictionary<string, object>();

        sessions.SyncData(new TestDataStore(true, records));

        Assert.Same(active, Assert.Single((List<RevenueFarmingSession>)records["_coop_revenue_farming_sessions"]));
        Assert.Same(terminal, sessions.Get(ended.QuestGiver, ended.StringId));
        var restored = new RevenueFarmingSessionStore(Mock.Of<IControllerIdProvider>());
        restored.SyncData(new TestDataStore(false, records));
        Assert.Same(active, restored.Get(ongoing));
        Assert.Null(restored.Get(ended));
    }

    [Fact]
    public void NewAgreementReleasesOnlyThatControllersHistoricalSessions()
    {
        var sessions = new RevenueFarmingSessionStore(Mock.Of<IControllerIdProvider>());
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var otherQuest = LinkedQuest("other");
        var otherTerminal = sessions.Register(otherQuest, ObjectHelper.SkipConstructor<Hero>(), "other");
        otherQuest._questState = QuestBase.QuestStates.Finalized;
        Quest previous = null;
        for (var i = 0; i < 5; i++)
        {
            var quest = LinkedQuest("quest_" + i);
            var current = sessions.Register(quest, hero, "owner");
            if (previous != null) Assert.Null(sessions.Get(previous));
            Assert.Same(otherTerminal, sessions.Get(otherQuest));
            quest._questState = QuestBase.QuestStates.Finalized;
            Assert.Same(current, sessions.Register(quest, hero, "owner"));
            Assert.Throws<InvalidOperationException>(() => sessions.Register(quest, hero, "different"));
            previous = quest;
        }
    }

    private static Quest LinkedQuest(string id)
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var issue = ObjectHelper.SkipConstructor<Issue>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest.StringId = id;
        quest.QuestGiver = giver;
        quest._questState = QuestBase.QuestStates.Ongoing;
        issue.StringId = id + "_issue";
        issue.IssueOwner = giver;
        issue.IssueQuest = quest;
        giver.Issue = issue;
        return quest;
    }

    [Fact]
    public void StateRoundTripPreservesPartialProgressAndLocalizedJournalVariables()
    {
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(TextObject), false).SetSurrogate(typeof(TextObjectSurrogate));
        model.Add(typeof(CampaignTime), false).SetSurrogate(typeof(CampaignTimeSurrogate));
        var text = new TextObject("{=revenue-test}{AMOUNT} collected in {VILLAGE}");
        text.SetTextVariable("AMOUNT", 120);
        text.SetTextVariable("VILLAGE", new TextObject("Danustica"));
        var state = new NetworkRevenueFarmingState
        {
            GiverId = "giver", QuestId = "quest", Revision = 7, RequestedDenars = 333,
            IsFinalizing = true, CollectionSettlementId = "village",
            MenuSettlementId = "town", RefreshPresentation = true,
            Villages = new[] { new RevenueFarmingVillageState
            {
                SettlementId = "village", CollectedAmount = 120, HourlyGain = 40,
                AdditionalProgress = 0.35f, EventOccurred = true,
            } },
            Journal = new[] { new IssueJournalEntry(new JournalLog(new CampaignTime(9123L), text, null, 1, 3)) },
        };

        using var stream = new MemoryStream();
        model.Serialize(stream, state);
        stream.Position = 0;
        var copy = (NetworkRevenueFarmingState)model.Deserialize(stream, null, typeof(NetworkRevenueFarmingState));

        Assert.Equal(7, copy.Revision);
        Assert.Equal(333, copy.RequestedDenars);
        Assert.Equal("town", copy.MenuSettlementId);
        Assert.True(copy.RefreshPresentation);
        Assert.True(copy.IsFinalizing);
        Assert.False(copy.Collecting);
        Assert.Equal(0.35f, Assert.Single(copy.Villages).AdditionalProgress);
        Assert.False(copy.Villages[0].Done);
        Assert.Equal(120, copy.Villages[0].CollectedAmount);
        Assert.Empty(copy.UsedIncidents);
        Assert.Null(copy.Incident);
        var log = Assert.Single(copy.Journal);
        Assert.Equal(9123, log.Time.NumTicks);
        Assert.Equal(120, log.Text.Attributes["AMOUNT"]);
        Assert.Equal("Danustica", ((TextObject)log.Text.Attributes["VILLAGE"]).Value);
    }

    [Fact]
    public void StaleChoiceIsRejectedBeforeExecutingVanillaOrChangingRevision()
    {
        var previousRole = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var quest = ObjectHelper.SkipConstructor<Quest>();
            quest._questState = QuestBase.QuestStates.Ongoing;
            var hero = ObjectHelper.SkipConstructor<Hero>();
            hero.PartyBelongedTo = ObjectHelper.SkipConstructor<MobileParty>();
            var session = new RevenueFarmingSession(quest, hero, "owner") { Revision = 9 };
            var sessions = new RevenueFarmingSessionStore(Mock.Of<IControllerIdProvider>());
            using var execution = new RevenueFarmingExecutionScope(Mock.Of<IMessageBroker>());
            var actions = new RevenueFarmingActions(sessions, Mock.Of<IObjectManager>(),
                Mock.Of<INetwork>(), execution);

            Assert.False(actions.Execute(session, new RequestRevenueFarmingAction
            {
                Action = RevenueFarmingAction.ResolveIncident, Revision = 8, Incident = "brawl_breaks_out", Option = 0,
            }));
            Assert.Equal(9, session.Revision);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
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
}
