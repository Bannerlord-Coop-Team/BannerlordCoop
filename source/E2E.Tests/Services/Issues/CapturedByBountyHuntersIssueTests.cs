using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

public class CapturedByBountyHuntersIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private bool journalsRegistered;

    public CapturedByBountyHuntersIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    private (string Giver, string Hideout) CreateIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var hideoutSettlementId = environment.CreateRegisteredObject<Settlement>();
        var hideoutId = environment.CreateRegisteredObject<Hideout>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideoutSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.GangLeader;
                    giver.Clan = null;
                    hideoutSettlement.SetSettlementComponent(hideout);
                    Campaign.Current.MainParty = party;
                    party.CurrentSettlement = settlement;
                    Game.Current.PlayerTroop = giver.CharacterObject;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    if (Campaign.Current.GameMenuManager.GetGameMenu("hideout_place") == null)
                    {
                        var menu = new GameMenu("hideout_place");
                        menu.Initialize(new TextObject("Hideout"), null, GameMenu.MenuOverlayType.None);
                        Campaign.Current.GameMenuManager.AddGameMenu(menu);
                    }
                    var minimumParties = Math.Max(1, Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
                    for (var i = 0; i < minimumParties; i++)
                    {
                        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
                        clan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                        var bandits = BanditPartyComponent.CreateBanditParty("bounty-bandits-" + i, clan,
                            hideout, false, null, new CampaignVec2(Vec2.Zero, true));
                        bandits.CurrentSettlement = hideoutSettlement;
                    }
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
                if (!journalsRegistered)
                {
                    new JournalLogsCampaignBehavior().RegisterEvents();
                    new IssuesCampaignBehavior().RegisterEvents();
                }
            });
        }
        journalsRegistered = true;
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
            PotentialIssueData.StartIssueDelegate create = (in PotentialIssueData _, Hero hero) => new Issue(hero, hideout);
            var potential = new PotentialIssueData(create, typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        return (giverId, hideoutSettlementId);
    }

    [Fact]
    public void CreationReplicatesTheSelectedHideoutAndGenerationToBothClients()
    {
        var fixture = CreateIssue();
        var sent = Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkCapturedByBountyHuntersIssueCreated>());
        Assert.Equal(fixture.Hideout, sent.HideoutId);
        Assert.False(string.IsNullOrEmpty(sent.IssueId));
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.Same(hideout, issue._hideout);
                Assert.Equal(sent.IssueId, issue.StringId);
                Assert.Equal(sent.DueTime, issue.IssueDueTime);
                Assert.Equal(sent.Difficulty, issue.IssueDifficultyMultiplier);
                Assert.True(client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
                Assert.Equal(sent.Generation, generation);
                client.Resolve<IMessageBroker>().Publish(null, sent);
                Assert.Same(issue, giver.Issue);
            });
        }
    }

    [Fact]
    public void AcceptedQuestCreatesAJournalOnlyForTheRecordedPlayer()
    {
        var fixture = CreateIssue();
        var owner = environment.Clients.First();
        owner.Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        CampaignTime due = default;
        environment.Server.Call(() =>
        {
            due = CampaignTime.DaysFromNow(30);
            var journal = new[] { new JournalLog(CampaignTime.Now, new TextObject("Accepted at the server settlement")) };
            var fields = GenericAcceptFieldsSerializer.Serialize(new BountyHuntersQuestAcceptFields(due, 0.25f, journal));
            environment.Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeQuestAccepted(fixture.Giver, "bounty-owner", fields));
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(giver.Issue.IsSolvingWithQuest);
                if (client == owner)
                {
                    var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                    Assert.True(quest.IsOngoing);
                    Assert.Equal(due, quest.QuestDueTime);
                    Assert.Single(quest.JournalEntries);
                    Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                }
                else
                {
                    Assert.Null(giver.Issue.IssueQuest);
                    Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, quest => quest is Quest);
                }
            });
        }
    }
    [Fact]
    public void AlternativeAcceptanceKeepsTheReturnJournalOnTheRecordedClient()
    {
        var fixture = CreateIssue();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var owner = environment.Clients.First();
        owner.Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            using (new AllowedThread())
            {
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(5), CampaignTime.DaysFromNow(4), 0.2f, 2, 1000, null);
            var journal = new[] { new JournalLog(CampaignTime.Now, new TextObject("The companion departed"), new TextObject("Return Days"), 0, 5) };
            var fields = GenericAcceptFieldsSerializer.Serialize(new BountyHuntersAlternativeAcceptFields(state, 0.25f, journal));
            var troops = environment.Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(giver.Issue.AlternativeSolutionSentTroops);
            environment.Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeAlternativeAccepted(fixture.Giver, "bounty-owner", state, fields, troops));
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(2, giver.Issue._alternativeSolutionCasualtyCount);
                if (client == owner) Assert.Single(giver.Issue.JournalEntries);
                else Assert.Empty(giver.Issue.JournalEntries);
            });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("previous-owner")]
    public void MalformedAcceptancePreservesThePreviousOwner(string previousOwner)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var owners = client.Resolve<IIssueOwnershipRegistry>();
            if (previousOwner != null) owners.SetOwner(giver, previousOwner);
            client.Resolve<IMessageBroker>().Publish(null, new NetworkQuestTypeQuestAccepted(fixture.Giver, "new-owner", new byte[] { 0x0f }));
            Assert.Equal(previousOwner != null, owners.TryGetOwnerControllerId(giver, out var restored));
            Assert.Equal(previousOwner, restored);
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
        });
    }

    private string AcceptFromFirstClient((string Giver, string Hideout) fixture, bool alternative = false, int clientIndex = 0)
    {
        var playerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var companionId = alternative ? environment.CreateRegisteredObject<Hero>() : null;
        var client = environment.Clients.ElementAt(clientIndex);
        var controller = clientIndex == 0 ? "bounty-owner" : "other-player";
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var hero));
            Assert.True(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(environment.Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                hero.Clan.SetLeader(hero);
                troop.Level = 20;
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                party.MemberRoster.AddToCounts(troop, 25);
                party.CurrentSettlement = giver.CurrentSettlement;
                if (alternative)
                {
                    Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                    companion.Clan = hero.Clan;
                    companion.CompanionOf = hero.Clan;
                    companion.PartyBelongedTo = party;
                    party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                }
            }
            Assert.True(environment.Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller, playerId, partyId, "", "")));
        });
        environment.ConnectRegisteredPlayer(client, controller);
        environment.Clients.First().Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Campaign.Current.MainParty = party;
            ResolvedMainHeroContext.ResolvedMainHero = hero;
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, controller));
            if (alternative)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                using (new AllowedThread())
                {
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 10);
                }
                giver.Issue.StartIssueWithAlternativeSolution();
            }
            else Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
        });
        if (alternative) Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>().Where(message => message.OwnerId == fixture.Giver));
        else Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>().Where(message => message.OwnerId == fixture.Giver));
        return playerId;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlternativeCompletionRetainsTheOwnersResultAndSurvivors(bool fails)
    {
        var fixture = CreateIssue();
        var playerId = AcceptFromFirstClient(fixture, alternative: true);
        var owner = environment.Clients.First();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var issue = Assert.IsType<Issue>(giver.Issue);
            Assert.Equal(11, issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.True(issue.AlternativeSolutionHero.IsDisabled);
            issue.JournalEntries[0].UpdateCurrentProgress(3);
        });
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.Equal(3, Assert.Single(giver.Issue.JournalEntries).CurrentProgress);
        });
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var player));
            var issue = Assert.IsType<Issue>(giver.Issue);
            var gold = player.Gold;
            var casualties = issue._alternativeSolutionCasualtyCount;
            issue._failureChance = fails ? 1f : -1f;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Now - CampaignTime.Days(1);
            issue.CompleteIssueWithAlternativeSolution();
            Assert.Null(giver.Issue);
            Assert.Equal(gold + (fails ? 0 : 3000), player.Gold);
            Assert.True(environment.Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("bounty-owner", out var returned));
            Assert.Equal(11 - casualties, returned.TotalManCount);
            Assert.Equal(1, returned.TotalHeroes);
            if (fails)
            {
                Assert.True(environment.Server.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>().TryGet(player, out var traits));
                Assert.Equal(-10, traits.GetPropertyValue(DefaultTraits.Honor));
            }
        });
        owner.Call(() =>
        {
            var journal = Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            Assert.True(journal.IsEnded());
            Assert.Equal(fails, journal.IsEndedUnsuccessfully());
            Assert.True(owner.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("bounty-owner", out var returned));
            Assert.Equal(1, returned.TotalHeroes);
        });
        environment.Clients.Last().Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()));
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool saving, Dictionary<string, object> records)
        {
            IsSaving = saving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving) records[key] = data;
            else if (records.TryGetValue(key, out var saved)) data = (T)saved;
            else return false;
            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadFiltersAnotherPlayersActiveAndFinishedJournals(bool completed)
    {
        var fixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        var owner = environment.Clients.First();
        Quest quest = null;
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            quest = (Quest)giver.Issue.IssueQuest;
        });
        if (completed)
        {
            environment.Server.Call(() =>
            {
                Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                giver.Issue.IssueQuest.CompleteQuestWithCancel();
            });
        }
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var journals = owner.Resolve<IBountyHuntersJournalOwners>();
            var saved = new Dictionary<string, object>();
            journals.SyncData(new TestDataStore(true, saved));
            journals.SyncData(new TestDataStore(false, new Dictionary<string, object>()));
            journals.SyncData(new TestDataStore(false, saved));
            journals.FilterLoadedCampaign();
            Assert.Equal(!completed, Campaign.Current.QuestManager.Quests.Contains(quest));
            Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());

            owner.Resolve<IControllerIdProvider>().SetControllerId("other-player");
            journals.FilterLoadedCampaign();
            Assert.Null(giver.Issue?.IssueQuest);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            Assert.DoesNotContain(Campaign.Current.QuestManager.TrackedObjects, entry => entry.Value.Contains(quest));
        });
    }

    [Fact]
    public void OldJournalUpdatesAndAnotherPlayersCompletionDoNotReplacePersonalProgress()
    {
        var fixture = CreateIssue();
        var otherFixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        AcceptFromFirstClient(otherFixture, clientIndex: 1);
        var owner = environment.Clients.First();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(otherFixture.Giver, out var other));
            Assert.True(environment.Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
            var stale = new MBList<JournalLog> { new JournalLog(CampaignTime.Now, new TextObject("Stale result")) };
            environment.Server.Resolve<INetwork>().SendAll(new NetworkBountyHuntersJournal(fixture.Giver, generation - 1,
                true, stale, IssueBase.IssueUpdateDetails.None));
            other.Issue.IssueQuest.CompleteQuestWithTimeOut();
        });
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.DoesNotContain(giver.Issue.IssueQuest.JournalEntries, log => log.LogText.ToString() == "Stale result");
            Assert.True(giver.Issue.IssueQuest.IsOngoing);
            Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
        });
    }

    [Fact]
    public void ASecondBountyRequestFromTheSamePlayerIsRejectedWithoutCancelingTheFirst()
    {
        var fixture = CreateIssue();
        var second = CreateIssue();
        AcceptFromFirstClient(fixture);
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(second.Giver, out var giver));
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, "bounty-owner"));
            Campaign.Current.IssueManager.StartIssueQuest(giver);
        });
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(second.Giver, out var secondGiver));
            Assert.True(giver.Issue.IssueQuest.IsOngoing);
            Assert.True(secondGiver.Issue.IsOngoingWithoutQuest);
            Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        });
    }

    [Fact]
    public void LeavingTheGiverTracksTheHideoutOnlyForItsOwnerAndFinalizationRemovesIt()
    {
        var fixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        string partyId = null;
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.Resolve<IPlayerManager>().TryGetPlayer("bounty-owner", out var player));
            partyId = player.MobilePartyId;
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                using (new AllowedThread()) party.CurrentSettlement = giver.CurrentSettlement;
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            LeaveSettlementAction.ApplyForParty(party);
            Assert.Null(party.CurrentSettlement);
        });
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.Equal(instance != environment.Clients.Last(), Campaign.Current.QuestManager.TrackedObjects.ContainsKey(hideout));
                if (instance != environment.Clients.Last())
                {
                    Assert.True(hideout.IsVisible);
                    Assert.True(hideout.Hideout.IsSpotted);
                }
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            giver.Issue.IssueQuest.CompleteQuestWithCancel();
        });
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.False(Campaign.Current.QuestManager.TrackedObjects.ContainsKey(hideout));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GiverDeathCancelsTheIssueAndRetainsDispatchedTroops(bool alternative)
    {
        var fixture = CreateIssue();
        if (alternative) AcceptFromFirstClient(fixture, alternative: true);
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Campaign.Current.IssueManager.OnHeroKilled(giver, null, default, false);
            Assert.Null(giver.Issue);
            var returns = environment.Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            Assert.Equal(alternative, returns.TryGet("bounty-owner", out var troops));
            if (alternative) Assert.Equal(11, troops.TotalManCount);
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.Null(giver.Issue);
                if (alternative && client == environment.Clients.First())
                    Assert.True(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEndedUnsuccessfully());
                else Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            });
        }
    }

    [Fact]
    public void JoiningPreservesTheLoadedQuestWhileARealHeirChangeCancelsOnlyItsOwnersQuest()
    {
        var fixture = CreateIssue();
        var otherFixture = CreateIssue();
        var playerId = AcceptFromFirstClient(fixture);
        AcceptFromFirstClient(otherFixture, clientIndex: 1);
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var quest = giver.Issue.IssueQuest;
            LeaveSettlementActionPatches.SuppressForPlayerSwitch = true;
            try
            {
                using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
                    Campaign.Current.QuestManager.OnPlayerCharacterChanged(giver, Hero.MainHero, MobileParty.MainParty, true);
            }
            finally
            {
                LeaveSettlementActionPatches.SuppressForPlayerSwitch = false;
            }
            Assert.True(quest.IsOngoing);
            Assert.Same(quest, giver.Issue.IssueQuest);
        });
        var heirId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var players = environment.Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("bounty-owner", out var player));
            Assert.Equal(playerId, player.HeroId);
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(heirId, out var heir));
            Assert.True(players.ReplacePlayer(player, new Player(player.ControllerId, heirId, player.MobilePartyId, "", "")));
            environment.Server.Resolve<IMessageBroker>().Publish(null, new PlayerHeirSelectionCompleted(heir));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(otherFixture.Giver, out var otherGiver));
            Assert.Null(giver.Issue);
            Assert.True(otherGiver.Issue.IssueQuest.IsOngoing);
        });
        client.Call(() => Assert.True(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEndedUnsuccessfully()));
        environment.Clients.Last().Call(() => Assert.False(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEnded()));
    }

    [Theory]
    [InlineData("success", 100, 3000, 0)]
    [InlineData("failure", -10, 0, 0)]
    [InlineData("timeout", -10, 0, 0)]
    [InlineData("success", 100, 3000, 1)]
    [InlineData("failure", -10, 0, 1)]
    [InlineData("timeout", -10, 0, 1)]
    [InlineData("success", 100, 3000, 2)]
    [InlineData("failure", -10, 0, 2)]
    [InlineData("timeout", -10, 0, 2)]
    public void TerminalConsequencesUseTheRecordedPlayerAndMirrorTheJournal(string outcome, int honorXp, int gold, int settlementCase)
    {
        var fixture = CreateIssue();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                using (new AllowedThread())
                {
                    giver.AddPower(100f - giver.Power);
                    giver.CurrentSettlement.Town.Security = 50f;
                }
            });
        }
        var playerId = AcceptFromFirstClient(fixture);
        var quests = new Dictionary<object, Quest>();
        foreach (var instance in new[] { environment.Server, environment.Clients.First() })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                quests.Add(instance, Assert.IsType<Quest>(giver.Issue.IssueQuest));
                Assert.Equal(giver.Issue.StringId + "_quest", giver.Issue.IssueQuest.StringId);
                Assert.Single(giver.Issue.IssueQuest.JournalEntries);
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var player));
            var beforeGold = player.Gold;
            var previousHero = Hero.MainHero;
            var previousParty = Campaign.Current.MainParty;
            var campaignXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
            var quest = quests[environment.Server];
            var town = quest.QuestGiver.CurrentSettlement.Town;
            var relation = quest.QuestGiver.GetRelation(player);
            if (settlementCase > 0)
            {
                Assert.True(environment.Server.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                using (new AllowedThread()) quest.QuestGiver.StayingInSettlement = settlementCase == 1 ? null : hideout;
            }
            Assert.True(environment.Server.Resolve<IBountyHuntersQuestContext>().TryOpen(quest.QuestGiver, out var scope));
            using (scope)
            {
                if (outcome == "success")
                {
                    quest.AddLog(quest.SuccessQuestLogText);
                    quest.SuccessConsequences();
                }
                else if (outcome == "failure")
                {
                    quest.AddLog(quest.PlayerLostTheFightLogText);
                    quest.FailConsequences(false);
                }
                else quest.CompleteQuestWithTimeOut();
            }
            Assert.True(quest.IsFinalized);
            Assert.Null(quest.QuestGiver.Issue);
            Assert.Equal(beforeGold + gold, player.Gold);
            Assert.Equal(100f + (outcome == "success" ? 10f : -10f), quest.QuestGiver.Power);
            Assert.Equal(50f + (settlementCase > 0 ? 0f : outcome == "success" ? -5f : 5f), town.Security);
            Assert.Equal(relation + (outcome == "success" ? 5f : -5f), quest.QuestGiver.GetRelation(player));
            Assert.True(environment.Server.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>().TryGet(player, out var progress));
            Assert.Equal(honorXp, progress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(campaignXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Same(previousHero, Hero.MainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.Equal(100f + (outcome == "success" ? 10f : -10f), giver.Power);
                Assert.Equal(50f + (settlementCase > 0 ? 0f : outcome == "success" ? -5f : 5f), giver.CurrentSettlement.Town.Security);
            });
        }
        var owner = environment.Clients.First();
        owner.Call(() =>
        {
            var quest = quests[owner];
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.IsRelatedTo(quest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Equal(outcome != "success", archive.IsEndedUnsuccessfully());
        });
        environment.Clients.Last().Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()));
    }
}
