using Common;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;

using GameInterface.Services.Issues.Quests.RevenueFarming;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Surrogates;
using HarmonyLib;
using Moq;
using ProtoBuf.Meta;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

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

    [Fact]
    public void StateRoundTripPreservesPartialProgressAndLocalizedJournalVariables()
    {
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(TextObject), false).SetSurrogate(typeof(TextObjectSurrogate));
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
            Journal = new[] { new RevenueFarmingJournalState { TimeTicks = 9123, Text = text, Progress = 1, Range = 3 } },
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
        Assert.Equal(9123, log.TimeTicks);
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
            var session = new RevenueFarmingSession(quest, ObjectHelper.SkipConstructor<Hero>(), "owner") { Revision = 9 };
            var sessions = new RevenueFarmingSessionStore(Mock.Of<IControllerIdProvider>());
            var actions = new RevenueFarmingActions(sessions, Mock.Of<IObjectManager>(),
                Mock.Of<INetwork>());

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

    [Fact]
    public void AllRevenuePatchesBindToInstalledGameMethods()
    {
        var harmony = new Harmony("RevenueFarmingTests.Binding");
        try
        {
            foreach (var type in typeof(RevenueFarmingSessionStore).Assembly.GetTypes().Where(type =>
                type.Namespace == typeof(RevenueFarmingSessionStore).Namespace &&
                type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0))
                harmony.CreateClassProcessor(type).Patch();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
