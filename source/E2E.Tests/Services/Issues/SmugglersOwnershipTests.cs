using Common;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using Moq;
using Common.Util;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using GameInterface.Services.Issues.Patches;
using E2E.Tests.Util;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.Smugglers;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class SmugglersOwnershipTests : SyncTestBase
{
    public SmugglersOwnershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AlternativeAcceptUsesServerJournalAndOwnerWithoutRunningFallbackMirror()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var playerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var originId = TestEnvironment.CreateRegisteredObject<Settlement>();
        long started = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(originId, out var origin));
            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new SmugglersIssueBehavior.SmugglersIssue(hero,
                    new KeyValuePair<Settlement, Settlement>(target, origin)),
                typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(8), CampaignTime.DaysFromNow(7), 0.2f, 2, 1100, null);
            var log = new JournalLog(CampaignTime.Now - CampaignTime.Days(1), new TextObject("server companion journal"),
                new TextObject("Return Days"), 0, 8);
            started = log.LogTime.NumTicks;
            var fields = new SmugglersAlternativeAcceptFields(0.2f, state, playerId, new SmugglersJournalEntry(log));
            Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeAlternativeAccepted(giverId, "smugglers-owner", state,
                GenericAcceptFieldsSerializer.Serialize(fields), new TroopRosterData(Array.Empty<TroopRosterElementData>())));
        });
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(playerId, out var player));
                var issue = Assert.IsType<SmugglersIssueBehavior.SmugglersIssue>(giver.Issue);
                Assert.True(issue.IsSolvingWithAlternative);
                Assert.True(client.Resolve<ISmugglersQuestOwners>().TryGet(issue, out var owner));
                Assert.Same(player, owner);
                var log = Assert.Single(issue.JournalEntries);
                Assert.Equal(started, log.LogTime.NumTicks);
                Assert.Equal("server companion journal", log.LogText.ToString());
                Assert.Equal(0.2f, issue.IssueDifficultyMultiplier);
            });
        }
    }

    [Fact]
    public void AnotherPlayersQuestDoesNotRegisterAVisibleMapMarker()
    {
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                quest.IsTrackEnabled = true;
                quest.AddTrackedObject(target);
                Assert.False(quest.IsTracked(target));
                Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(target));
                client.Resolve<ISmugglersQuestOwners>().Set(quest, Hero.MainHero);
                quest.AddTrackedObject(target);
                Assert.True(quest.IsTracked(target));
                Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(target));
            });
        }
    }

    [Fact]
    public void ConversationChecksOwnershipBeforeInvokingAnyQuestLineCondition()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var owner = Clients.First();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                client.Resolve<IControllerIdProvider>().SetControllerId(client == owner ? "smugglers-owner" : "other-player");
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                client.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "smugglers-owner");
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                quest.QuestGiver = giver;
                var calls = 0;
                var sentence = new ConversationSentence("smugglers_persuasion_attempt", TextObject.GetEmpty(),
                    "smugglers_persuasion_start_reservation", "smugglers_persuasion_select_option",
                    () => { calls++; return true; }, null, null, relatedObject: quest);

                Assert.Equal(client == owner, sentence.RunCondition());
                Assert.Equal(client == owner ? 1 : 0, calls);
            });
        }
    }

    [Fact]
    public void ReceivingWorldChangesDoesNotRunLocalQuestAcceptanceOrCancellation()
    {
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
                var issue = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssue>();
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                using (new AllowedThread())
                {
                    quest.QuestAcceptedConsequences();
                    quest.CompleteQuestWithCancel(new TextObject("world callback"));
                    issue.CompleteIssueWithCancel();
                }
                Assert.True(quest.IsOngoing);
                Assert.Null(quest._smugglerParty);
                Assert.True(issue.IsSolvingWithAlternative);
            });
        }
    }

    [Fact]
    public void LosingAlternativeSelectionSurvivesWinnerMirrorAndReturnsExactlyOnce()
    {
        var fixture = CreateIssue();
        var loser = Clients.First();
        var loserPlayer = RegisterPlayer(loser, "loser");
        var winner = RegisterPlayer(Clients.Last(), "winner");
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var winnerCompanionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var router = Server.Resolve<TestNetworkRouter>();
        router.PauseLink(loser.NetPeer, Server.NetPeer);
        var generation = 0;
        loser.Call(() =>
        {
            var giver = Get<Hero>(loser, fixture.Giver);
            var companion = Get<Hero>(loser, companionId).CharacterObject;
            using (new AllowedThread())
            {
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion, 1);
            }
            Assert.Equal(0, MobileParty.MainParty.MemberRoster.GetTroopCount(companion));
            Assert.True(loser.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out generation));
            giver.Issue.StartIssueWithAlternativeSolution();
            giver.Issue.StartIssueWithAlternativeSolution();
        });
        Assert.Single(loser.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
        Server.Call(() =>
        {
            var companion = Get<Hero>(Server, winnerCompanionId);
            var roster = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread()) roster.AddToCounts(companion.CharacterObject, 1);
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(8), CampaignTime.DaysFromNow(7), 0.2f, 0, 1000, null);
            var fields = new SmugglersAlternativeAcceptFields(0.2f, state, winner.HeroId,
                new SmugglersJournalEntry(new JournalLog(CampaignTime.Now, new TextObject("winner journal"))));
            var network = Server.Resolve<INetwork>();
            network.SendAll(new NetworkQuestTypeAlternativeAccepted(fixture.Giver, winner.ControllerId, state,
                GenericAcceptFieldsSerializer.Serialize(fields), Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(roster), generation));
            network.Send(loser.NetPeer, new NetworkQuestTypeAcceptRejected(fixture.Giver, true, generation - 1));
        });
        loser.PumpGameThread();
        loser.Call(() => Assert.Equal(0, MobileParty.MainParty.MemberRoster.GetTroopCount(Get<Hero>(loser, companionId).CharacterObject)));
        Server.Call(() =>
        {
            var rejected = new NetworkQuestTypeAcceptRejected(fixture.Giver, true, generation);
            Server.Resolve<INetwork>().Send(loser.NetPeer, rejected);
            Server.Resolve<INetwork>().Send(loser.NetPeer, rejected);
        });
        loser.PumpGameThread();
        loser.Call(() =>
        {
            var issue = Get<Hero>(loser, fixture.Giver).Issue;
            Assert.Equal(1, MobileParty.MainParty.MemberRoster.GetTroopCount(Get<Hero>(loser, companionId).CharacterObject));
            Assert.Equal(0, MobileParty.MainParty.MemberRoster.GetTroopCount(Get<Hero>(loser, winnerCompanionId).CharacterObject));
            Assert.Equal(1, issue.AlternativeSolutionSentTroops.GetTroopCount(Get<Hero>(loser, winnerCompanionId).CharacterObject));
            Assert.True(issue.IsSolvingWithAlternative);
            Assert.True(loser.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(issue.IssueOwner, out var owner));
            Assert.Equal(winner.ControllerId, owner);
        });
    }

    [Fact]
    public void TimeoutEmitsOneTimeoutAndOnePenaltyOnServerAndBothClients()
    {
        var fixture = CreateIssue();
        var player = RegisterPlayer(Clients.First(), "timeout-owner");
        var quests = SetupDirectQuest(fixture, player);
        var observed = new Dictionary<EnvironmentInstance, List<QuestBase.QuestCompleteDetails>>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var events = observed[instance] = new List<QuestBase.QuestCompleteDetails>();
                CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(events,
                    (quest, reason) => { if (quest == quests[instance]) events.Add(reason); });
            });
        }
        Server.Call(() =>
        {
            Server.Resolve<QuestTraitProgressHandler>().Store(Get<Hero>(Server, player.HeroId), new Dictionary<string, int>
            {
                [DefaultTraits.Honor.StringId] = 20,
                [DefaultTraits.Valor.StringId] = 30,
            });
            quests[Server].CompleteQuestWithTimeOut();
        });
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients) client.PumpGameThread();
        Assert.Equal(IssueFinalizeReason.QuestTimeout, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>()).Reason);
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestTraitProgress>().Count());
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.Equal(QuestBase.QuestCompleteDetails.Timeout, Assert.Single(observed[instance]));
                Assert.Null(Get<Hero>(instance, fixture.Giver).Issue);
                Assert.False(quests[instance].IsOngoing);
            });
        }
        Clients.First().Call(() =>
        {
            Assert.Equal(-30, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(-20, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Valor));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PermanentRemovalCancelsOnlyOldOwnersCommitmentAndClearsReturns(bool alternative, bool offline)
    {
        var fixture = CreateIssue();
        var player = RegisterPlayer(Clients.First(), "removed-owner");
        var other = RegisterPlayer(Clients.Last(), "other-owner");
        if (!alternative) SetupDirectQuest(fixture, player);
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var replacementPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, fixture.Giver);
            var companion = Get<Hero>(Server, companionId);
            using (new AllowedThread())
            {
                if (alternative)
                {
                    giver.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                }
                var otherRoster = TroopRoster.CreateDummyTroopRoster();
                otherRoster.AddToCounts(Get<Hero>(Server, other.HeroId).CharacterObject, 1);
                Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(other.ControllerId, otherRoster);
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, player.ControllerId);
            if (alternative) Server.Resolve<ISmugglersQuestOwners>().Set(giver.Issue, Get<Hero>(Server, player.HeroId));
            if (offline) Server.Resolve<IPlayerManager>().ClearPeer(Clients.First().NetPeer);
            Assert.True(Server.Resolve<IPlayerManager>().RemovePlayer(player));
            Assert.Null(giver.Issue);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(player.ControllerId, out _));
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(other.ControllerId, out _));
            if (alternative)
            {
                Assert.True(companion.IsActive);
                Assert.Equal(1, Get<MobileParty>(Server, player.MobilePartyId).MemberRoster.GetTroopCount(companion.CharacterObject));
            }
            var replacementHero = Get<MobileParty>(Server, replacementPartyId).LeaderHero;
            Assert.True(Server.ObjectManager.TryGetId(replacementHero, out var replacementHeroId));
            Assert.True(Server.ObjectManager.TryGetId(replacementHero.Clan, out var replacementClanId));
            Assert.True(Server.ObjectManager.TryGetId(replacementHero.CharacterObject, out var replacementCharacterId));
            var replacement = new Player(player.ControllerId, replacementHeroId, replacementPartyId, replacementClanId, replacementCharacterId);
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(replacement));
            Campaign.Current.IssueManager.DailyTick();
            Assert.Null(giver.Issue);
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(replacement.ControllerId, out _));
        });
        foreach (var client in Clients) client.PumpGameThread();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestPlayerRemoved>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingHeroValidationCancelsOldCommitmentOnGameThreadBeforeCharacterCreation(bool alternative)
    {
        var fixture = CreateIssue();
        var player = RegisterPlayer(Clients.First(), "missing-owner");
        var other = RegisterPlayer(Clients.Last(), "surviving-owner");
        if (!alternative) SetupDirectQuest(fixture, player);
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var replacementPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var journal = new JournalLogsCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { journal }));
            journal.RegisterEvents();
            var giver = Get<Hero>(Server, fixture.Giver);
            var companion = Get<Hero>(Server, companionId);
            using (new AllowedThread())
            {
                var returns = TroopRoster.CreateDummyTroopRoster();
                returns.AddToCounts(companion.CharacterObject, 1);
                companion.ChangeState(Hero.CharacterStates.Disabled);
                if (alternative)
                {
                    giver.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    giver.Issue.AlternativeSolutionSentTroops.Add(returns);
                }
                else
                    Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(player.ControllerId, returns);
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, player.ControllerId);
            var removedIssue = giver.Issue;
            removedIssue.IsTriedToSolveBefore = true;
            Game.Current.PlayerTroop = null;
            Campaign.Current.MainParty = null;
            Campaign.Current.PlayerDefaultFaction = null;
            Assert.True(Server.ObjectManager.Remove(Get<Hero>(Server, player.HeroId)));
            Assert.True(Server.ObjectManager.Remove(Get<MobileParty>(Server, player.MobilePartyId)));
            var notifications = new List<Hero>();
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(notifications, (issue, detail, solver) =>
            {
                if (issue.IssueOwner != giver || detail != IssueBase.IssueUpdateDetails.IssueCancel) return;
                Assert.True(GameThread.Instance.IsGameThread);
                notifications.Add(solver);
            });
            var connection = new Mock<IConnectionLogic>();
            connection.SetupGet(value => value.Peer).Returns(Clients.First().NetPeer);
            var validationNetwork = new Mock<INetwork>();
            using var state = new ResolveCharacterState(connection.Object, Server.Resolve<IMessageBroker>(), validationNetwork.Object,
                Mock.Of<IModuleValidator>(), Server.Resolve<IPlayerManager>(), Mock.Of<IPlayerPartyRestorer>(),
                Server.ObjectManager, Mock.Of<IModuleInfoProvider>(), Mock.Of<IExistingPlayerSender>(),
                Mock.Of<ISteamBanList>(), Mock.Of<IJoinValidationDenialLog>());
            var validation = Task.Run(() => state.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                Clients.First().NetPeer, new NetworkClientValidate(player.ControllerId))));
            Assert.True(SpinWait.SpinUntil(() =>
            {
                GameThread.Instance.Update(TimeSpan.Zero);
                return validation.IsCompleted;
            }, TimeSpan.FromSeconds(10)));
            validation.GetAwaiter().GetResult();
            connection.Verify(value => value.CreateCharacter(), Times.Once);
            validationNetwork.Verify(value => value.SendImmediate(Clients.First().NetPeer,
                It.Is<NetworkClientValidated>(message => !message.HeroExists && message.Player == null)), Times.Once);
            Assert.Null(Assert.Single(notifications));
            Assert.Null(giver.Issue);
            Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel,
                Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>().GetRelatedLog(removedIssue)._lastIssueStatus);
            Assert.Null(Game.Current.PlayerTroop);
            Assert.Null(Campaign.Current.MainParty);
            Assert.False(Server.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out _));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(other.ControllerId, out _));
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(player.ControllerId, out _));
            Assert.True(companion.IsActive);
            Assert.Equal(0, Get<MobileParty>(Server, other.MobilePartyId).MemberRoster.GetTroopCount(companion.CharacterObject));
            var replacement = Get<MobileParty>(Server, replacementPartyId);
            Assert.True(Server.ObjectManager.TryGetId(replacement.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(replacement.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(replacement.LeaderHero.CharacterObject, out var characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(player.ControllerId, heroId, replacementPartyId, clanId, characterId)));
            Campaign.Current.IssueManager.DailyTick();
            Assert.Equal(0, replacement.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(player.ControllerId, out _));
        });
        foreach (var client in Clients) client.PumpGameThread();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestPlayerRemoved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTraitProgress>());
    }

    [Fact]
    public void QueuedRemovalThatLosesToRegistrationReplacementDoesNotCancelItsQuest()
    {
        var fixture = CreateIssue();
        var player = RegisterPlayer(Clients.First(), "replaced-owner");
        SetupDirectQuest(fixture, player);
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            var removal = Task.Run(() => players.RemovePlayer(player));
            Assert.True(SpinWait.SpinUntil(() => Server.PendingGameThreadActionCount > 0 || removal.IsCompleted,
                TimeSpan.FromSeconds(10)));
            var replacement = new Player(player.ControllerId, player.HeroId, player.MobilePartyId, player.ClanId, player.CharacterObjectId);
            Assert.True(players.ReplacePlayer(player, replacement));
            Assert.True(SpinWait.SpinUntil(() =>
            {
                GameThread.Instance.Update(TimeSpan.Zero);
                return removal.IsCompleted;
            }, TimeSpan.FromSeconds(10)));
            Assert.False(removal.GetAwaiter().GetResult());
            Assert.True(players.TryGetPlayer(player.ControllerId, out var current));
            Assert.Same(replacement, current);
            Assert.True(Get<Hero>(Server, fixture.Giver).Issue.IssueQuest.IsOngoing);
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestPlayerRemoved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
    }

    [Fact]
    public void ClearedReturnInquiryCannotGiveOldTroopsToARecreatedCharacter()
    {
        var client = Clients.First();
        var player = RegisterPlayer(client, "return-owner");
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var replacementPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        object captured = null;
        var capture = AwaitingAlternativeSolutionTroopsTests.InquiryCaptureHandler.MakeDelegate(inquiry => captured = inquiry);
        AwaitingAlternativeSolutionTroopsTests.InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, capture);
        try
        {
            client.Call(() =>
            {
                using var scope = new AllowedThread();
                Hero.MainHero.ChangeState(Hero.CharacterStates.Active);
                var roster = TroopRoster.CreateDummyTroopRoster();
                roster.AddToCounts(Get<Hero>(client, companionId).CharacterObject, 1);
                client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(player.ControllerId, roster);
                IssueManagerAlternativeSolutionTroopsPatches.TryCheckIfTroopsCanReturnToMainParty();
            });
            Assert.NotNull(captured);
            Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkQuestPlayerRemoved(player.ControllerId, player.HeroId)));
            client.PumpGameThread();
            client.Call(() =>
            {
                Campaign.Current.MainParty = Get<MobileParty>(client, replacementPartyId);
                var before = MobileParty.MainParty.MemberRoster.TotalManCount;
                AwaitingAlternativeSolutionTroopsTests.InquiryCaptureHandler.InvokeAffirmativeAction(captured);
                Assert.Equal(before, MobileParty.MainParty.MemberRoster.TotalManCount);
                Assert.False(client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(player.ControllerId, out _));
            });
            Assert.Empty(client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
        }
        finally
        {
            AwaitingAlternativeSolutionTroopsTests.InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, capture);
        }
    }

    private (string Giver, string Target, string Origin) CreateIssue()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var originId = TestEnvironment.CreateRegisteredObject<Settlement>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            });
        }
        Server.Call(() =>
        {
            var potential = new PotentialIssueData(
                (in PotentialIssueData _, Hero giver) => new SmugglersIssueBehavior.SmugglersIssue(giver,
                    new KeyValuePair<Settlement, Settlement>(Get<Settlement>(Server, targetId), Get<Settlement>(Server, originId))),
                typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, Get<Hero>(Server, giverId)));
        });
        return (giverId, targetId, originId);
    }

    private Player RegisterPlayer(EnvironmentInstance client, string controller)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Player player = null;
        Server.Call(() =>
        {
            var hero = Get<MobileParty>(Server, partyId).LeaderHero;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            player = new Player(controller, heroId, partyId, clanId, characterId);
        });
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var hero = Get<Hero>(instance, player.HeroId);
                hero.Clan._banner = new Banner();
                var registered = instance == Server ? player : new Player(controller, player.HeroId, partyId, player.ClanId, player.CharacterObjectId);
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(registered));
                if (instance != client) return;
                instance.Resolve<IControllerIdProvider>().SetControllerId(controller);
                Game.Current.PlayerTroop = hero.CharacterObject;
                Campaign.Current.MainParty = Get<MobileParty>(instance, partyId);
                Campaign.Current.PlayerDefaultFaction = hero.Clan;
            });
        }
        TestEnvironment.ConnectRegisteredPlayer(client, controller);
        return player;
    }

    private Dictionary<EnvironmentInstance, SmugglersIssueBehavior.SmugglersIssueQuest> SetupDirectQuest(
        (string Giver, string Target, string Origin) fixture, Player player)
    {
        var quests = new Dictionary<EnvironmentInstance, SmugglersIssueBehavior.SmugglersIssueQuest>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var giver = Get<Hero>(instance, fixture.Giver);
                var quest = new SmugglersIssueBehavior.SmugglersIssueQuest("smugglers-test-quest", giver,
                    Get<Settlement>(instance, fixture.Target), Get<Settlement>(instance, fixture.Origin), 0.2f, CampaignTime.Now, 1350);
                var party = Get<MobileParty>(instance, partyId);
                party.IsActive = false;
                quest._smugglerParty = party;
                giver.Issue.IssueQuest = quest;
                giver.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                instance.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, player.ControllerId);
                instance.Resolve<ISmugglersQuestOwners>().Set(quest, Get<Hero>(instance, player.HeroId));
                quests[instance] = quest;
            });
        }
        return quests;
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
