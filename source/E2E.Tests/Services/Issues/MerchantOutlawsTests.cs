using Common.Messaging;
using Common.Util;
using Common.Network.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using Moq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class MerchantOutlawsTests : IDisposable
{
    private static readonly MethodBase[] TextHelpers =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string townId;
    private readonly string settlementId;
    private readonly string hideoutId;
    private readonly string hideoutSettlementId;
    private readonly string kingdomId;
    private readonly string giverClanId;
    private readonly string playerClanId;

    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance ClientA => environment.Clients.First();
    private EnvironmentInstance ClientB => environment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public MerchantOutlawsTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        townId = environment.CreateRegisteredObject<Town>();
        settlementId = environment.CreateRegisteredObject<Settlement>();
        hideoutId = environment.CreateRegisteredObject<Hideout>();
        hideoutSettlementId = environment.CreateRegisteredObject<Settlement>();
        kingdomId = environment.CreateRegisteredObject<Kingdom>();
        giverClanId = environment.CreateRegisteredObject<Clan>();
        playerClanId = environment.CreateRegisteredObject<Clan>();

        scenario = new QuestScenario(environment, (instance, giver) =>
        {
            var settlement = Get<Settlement>(instance, settlementId);
            var town = Get<Town>(instance, townId);
            var hideout = Get<Hideout>(instance, hideoutId);
            var hideoutSettlement = Get<Settlement>(instance, hideoutSettlementId);
            var giverClan = Get<Clan>(instance, giverClanId);
            settlement.SetSettlementComponent(town);
            town.OwnerClan = giverClan;
            town.Prosperity = 500f;
            giverClan._kingdom = Get<Kingdom>(instance, kingdomId);
            giver.StayingInSettlement = settlement;
            giver.Clan = giverClan;
            giver.Occupation = Occupation.Merchant;
            hideoutSettlement.SetSettlementComponent(hideout);

            var count = Math.Max(10, Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
            for (var i = 0; i < count; i++)
            {
                var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
                banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                var party = BanditPartyComponent.CreateBanditParty($"brigands_{i}", banditClan,
                    hideout, false, null, new CampaignVec2(Vec2.Zero, true));
                party.CurrentSettlement = hideoutSettlement;
                Assert.True(instance.ObjectManager.AddExisting(party.StringId, party));
            }

            Assert.True(hideout.IsInfested);
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            return new Issue(giver, hideout);
        });

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    foreach (var player in new[] { scenario.PlayerA, scenario.PlayerB })
                    {
                        var hero = Get<Hero>(instance, player.HeroId);
                        var party = Get<MobileParty>(instance, player.MobilePartyId);
                        hero._partyBelongedTo = party;
                        party.LordPartyComponent._leader = hero;
                        party.CurrentSettlement = Get<Settlement>(instance, settlementId);
                        hero.Gold = 100;
                    }

                    var localPlayer = instance == ClientB ? scenario.PlayerB : scenario.PlayerA;
                    Campaign.Current.MainParty = Get<MobileParty>(instance, localPlayer.MobilePartyId);
                    if (instance != Server)
                    {
                        Game.Current.PlayerTroop = Get<Hero>(instance, localPlayer.HeroId).CharacterObject;
                        Campaign.Current.PlayerDefaultFaction = Get<Hero>(instance, localPlayer.HeroId).Clan;
                    }
                }

                new IssuesCampaignBehavior().RegisterEvents();
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompanionReturnProgressIsAuthoritativeInEitherSnapshotAndDailyTickOrder(bool snapshotFirst)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var issue = Get<Hero>(instance, scenario.NotableId).Issue;
                using (new AllowedThread())
                {
                    issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(10);
                    issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Now;
                    issue._areIssueEffectsResolved = false;
                    issue._journalEntries.Clear();
                    issue._journalEntries.Add(new JournalLog(CampaignTime.Now, new TaleWorlds.Localization.TextObject("returning"),
                        currentProgress: instance == Server ? 3 : 2, range: 10));
                }
                instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, issue.StringId, QuestScenario.ControllerA);
            });
        }
        void Snapshot() => Server.Call(() => Server.Resolve<IMerchantOutlawsProgress>().Send(Get<Hero>(Server, scenario.NotableId).Issue));
        void Tick()
        {
            foreach (var client in new[] { ClientA, ClientB })
                client.Call(() => Campaign.Current.IssueManager.DailyTick(), TextHelpers);
        }

        if (snapshotFirst)
        {
            Snapshot();
            Tick();
        }
        else
        {
            Tick();
            ClientA.Call(() => Assert.Equal(2, Get<Hero>(ClientA, scenario.NotableId).Issue.JournalEntries[0].CurrentProgress));
            Snapshot();
        }
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            Assert.True(issue.IsSolvingWithAlternative);
            Assert.Equal(3, Assert.Single(issue.JournalEntries).CurrentProgress);
        });
        foreach (var client in new[] { ClientA, ClientB })
            client.Call(() =>
            {
                var issue = Get<Hero>(client, scenario.NotableId).Issue;
                Assert.True(issue._areIssueEffectsResolved);
                Assert.True(issue.IsSolvingWithAlternative);
                Assert.Single(issue.JournalEntries);
                Assert.Equal(client == ClientA ? 3 : 2, issue.JournalEntries[0].CurrentProgress);
            });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>());
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>());
    }

    [Fact]
    public void HideoutBanditsStayUntrackedWhenAcceptedAndProgressIsReplayed()
    {
        Accept();
        string[] hideoutParties = null;
        Server.Call(() =>
        {
            var quest = Quest(Server);
            hideoutParties = quest._validPartiesList.Where(party => party.CurrentSettlement ==
                Get<Settlement>(Server, hideoutSettlementId)).Select(party =>
                {
                    Assert.False(quest.IsTracked(party));
                    Assert.True(Server.ObjectManager.TryGetId(party, out var id));
                    return id;
                }).ToArray();
            Assert.NotEmpty(hideoutParties);
            Server.Resolve<IMerchantOutlawsProgress>().Send(quest);
        });
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            foreach (var id in hideoutParties)
            {
                var party = Get<MobileParty>(ClientA, id);
                Assert.Contains(party, Quest(ClientA)._validPartiesList);
                Assert.False(Quest(ClientA).IsTracked(party));
                Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(party));
            }
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void HourlyBanditAdditionsSendOneFinalSnapshotFromAnEmptyTargetList(int count)
    {
        Accept();
        var partyIds = Enumerable.Range(0, count).Select(_ => environment.CreateRegisteredObject<MobileParty>()).ToArray();
        var clanId = environment.CreateRegisteredObject<Clan>();
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Quest(Server).RemoveAllTrackedObjects();
                Quest(Server)._validPartiesList.Clear();
                var clan = Get<Clan>(Server, clanId);
                clan.IsBanditFaction = true;
                foreach (var id in partyIds)
                {
                    var party = Get<MobileParty>(Server, id);
                    party.IsBandit = true;
                    party.ActualClan = clan;
                    party.CurrentSettlement = Get<Settlement>(Server, settlementId);
                }
            }
        });
        var sentBefore = Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();
        var ownerBefore = ClientA.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();

        Server.Call(() => Server.Resolve<IMerchantOutlawsHourlyTick>().Tick(
            partyIds.Select(id => Get<MobileParty>(Server, id))), TextHelpers);
        environment.FlushCoalescer();

        var snapshot = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Skip(sentBefore));
        Assert.Equal(partyIds.OrderBy(id => id), snapshot.PartyIds.OrderBy(id => id));
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>().Skip(ownerBefore));
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>());
        Server.Call(() => Assert.Equal(count, Quest(Server)._validPartiesList.Count));
        ClientA.Call(() => Assert.Equal(count, Quest(ClientA)._validPartiesList.Count));
    }

    [Fact]
    public void CreationUsesTheServersHideoutAndIdentityOnBothClients()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            using (new AllowedThread()) giver.StayingInSettlement = Get<Settlement>(Server, settlementId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) =>
                new Issue(owner, Get<Hideout>(Server, hideoutId)), typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var issue = Assert.IsType<Issue>(Get<Hero>(instance, giverId).Issue);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(Get<Hideout>(instance, hideoutId), issue.RelatedHideout);
            });
        }
    }

    [Fact]
    public void RealAcceptKeepsTheQuestAndJournalOnItsOwner()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        ClientA.Call(() => Assert.Contains(Quest(ClientA), Campaign.Current.QuestManager.Quests));
        Server.Call(() => Assert.DoesNotContain(Quest(Server), Campaign.Current.QuestManager.Quests));
        ClientB.Call(() => Assert.Null(Get<Hero>(ClientB, scenario.NotableId).Issue.IssueQuest));
        Assert.Equal(Quest(Server)._totalPartyCount, Quest(ClientA)._totalPartyCount);
        Assert.Equal(0, Quest(ClientA)._questProgressLogTest.CurrentProgress);
    }

    [Fact]
    public void AnOfferClosedAfterItsCancellationCannotStartAnOrphanQuest()
    {
        Accept(startQuest: false);
        var ownerQuest = Quest(ClientA);
        ClientA.Call(() => Assert.Contains(Campaign.Current.ConversationManager._sentences,
            sentence => ReferenceEquals(sentence.RelatedObject, ownerQuest)));
        Server.Call(() => Quest(Server).CompleteQuestWithCancel(), TextHelpers);
        AssertEndedEverywhere();
        ClientA.Call(() =>
        {
            ownerQuest.QuestAcceptedConsequences();
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
            Assert.Null(ownerQuest._questProgressLogTest);
            Assert.False(ownerQuest.IsOngoing);
            Assert.DoesNotContain(Campaign.Current.ConversationManager._sentences,
                sentence => ReferenceEquals(sentence.RelatedObject, ownerQuest));
        }, TextHelpers);

        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, scenario.NotableId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) =>
                new Issue(owner, Get<Hideout>(Server, hideoutId)), typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        ClientA.Call(() =>
        {
            var player = Get<Hero>(ClientA, scenario.PlayerA.HeroId);
            using (new MainHeroSubstitutionScope(player, player.PartyBelongedTo))
            {
                var giver = Get<Hero>(ClientA, scenario.NotableId);
                giver.Issue.StartIssueWithQuest();
                var quest = Quest(ClientA);
                var manager = Campaign.Current.ConversationManager;
                var agent = new Mock<IAgent>();
                agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
                manager._conversationAgents.Add(agent.Object);
                var offer = manager._sentences.First(sentence =>
                    sentence.InputToken == manager.GetStateIndex("issue_classic_quest_start") && sentence.RunCondition());
                Assert.Same(quest, offer.RelatedObject);
                offer.RunConsequence(Game.Current);
                Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                Assert.NotNull(quest._questProgressLogTest);
            }
        }, TextHelpers);
    }

    [Fact]
    public void DestroyingATargetUpdatesOnlyTheOwnerAndCountsItOnce()
    {
        Accept();
        var targetId = TargetIds().First();
        Destroy(targetId, scenario.PlayerB);
        Assert.Equal(0, Quest(ClientA)._destroyedPartyCount);
        Assert.DoesNotContain(targetId, TargetIds());

        var ownerTarget = TargetIds().First();
        Destroy(ownerTarget, scenario.PlayerA);
        Destroy(ownerTarget, scenario.PlayerA);
        Assert.Equal(1, Quest(Server)._destroyedPartyCount);
        Assert.Equal(1, Quest(ClientA)._questProgressLogTest.CurrentProgress);
        Assert.Empty(ClientB.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Fact]
    public void OnlyTheOwnerIsToldAboutEachBandItHunts()
    {
        using var notices = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        Accept();
        var targets = TargetIds();
        Assert.True(Quest(Server)._totalPartyCount > 1);

        Destroy(targets[0], scenario.PlayerB);
        Assert.Equal(0, notices.Count);

        Destroy(targets[1], scenario.PlayerA);
        Assert.Equal(1, notices.CountFor(ClientA));
        Assert.Equal(1, notices.Count);
    }

    [Fact]
    public void TargetChangesMarkOnlyTheOwnersMap()
    {
        Accept();
        var sentBefore = Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();
        var ownerBefore = ClientA.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();
        Server.Call(() => Server.Resolve<IMerchantOutlawsProgress>().Send(Quest(Server)));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Skip(sentBefore));
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>().Skip(ownerBefore));
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>());
        var targetId = TargetIds().First();
        var otherHideoutId = environment.CreateRegisteredObject<Hideout>();
        var otherSettlementId = environment.CreateRegisteredObject<Settlement>();
        Server.Call(() =>
        {
            using (new AllowedThread()) Get<Settlement>(Server, otherSettlementId).SetSettlementComponent(Get<Hideout>(Server, otherHideoutId));
        });

        Server.Call(() => CampaignEventDispatcher.Instance.OnSettlementLeft(
            Get<MobileParty>(Server, targetId), Get<Settlement>(Server, hideoutSettlementId)), TextHelpers);
        Assert.DoesNotContain(targetId, TargetIds());
        ClientA.Call(() => Assert.DoesNotContain(Get<MobileParty>(ClientA, targetId), Quest(ClientA)._validPartiesList));

        // Traffic through another hideout changes no target, so the owner is not sent anything.
        var updates = Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, targetId);
            var other = Get<Settlement>(Server, otherSettlementId);
            CampaignEventDispatcher.Instance.OnSettlementEntered(party, other, null);
            CampaignEventDispatcher.Instance.OnSettlementLeft(party, other);
        }, TextHelpers);
        Assert.DoesNotContain(targetId, TargetIds());
        Assert.Equal(updates, Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count());

        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, targetId);
            using (new AllowedThread())
            {
                party.ActualClan.IsBanditFaction = true;
                party.CurrentSettlement = Get<Settlement>(Server, settlementId);
            }

            Server.Resolve<IMerchantOutlawsHourlyTick>().Tick(new[] { party });
        }, TextHelpers);

        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, targetId);
            var quest = Quest(Server);
            Assert.Contains(party, quest._validPartiesList);
            Assert.True(quest.IsTracked(party));
            // Joining clients load both managers with the save, so the server's targeting must not mark anything there.
            Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(party));
            Assert.Empty(CampaignUIHelper.GetQuestsRelatedToParty(party));
        });
        ClientA.Call(() =>
        {
            var party = Get<MobileParty>(ClientA, targetId);
            Assert.Contains(party, Quest(ClientA)._validPartiesList);
            Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(party));
        });
        ClientB.Call(() => Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(Get<MobileParty>(ClientB, targetId))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DestroyedMixedAndRecruitedEndingsPayOnlyTheOwner(int ending)
    {
        Accept();
        var serverQuest = Quest(Server);
        var ownerQuest = Quest(ClientA);
        var required = serverQuest._totalPartyCount;
        var targets = TargetIds().Take(required).ToArray();
        for (var i = 0; i < targets.Length; i++)
        {
            if (ending == 2 || (ending == 1 && i == 0)) Recruit(targets[i], scenario.PlayerA);
            else Destroy(targets[i], scenario.PlayerA);
        }

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(required, ownerQuest._questProgressLogTest.CurrentProgress);
        Assert.True(ownerQuest.IsFinalized);
        Assert.Single(ownerQuest.JournalEntries.Where(log => log.LogText?.ToString().Contains(
            ending == 0 ? "defeated all" : ending == 1 ? "recruited the rest" : "recruited the brigands") == true));
        Server.Call(() =>
        {
            Assert.Equal(100 + serverQuest.RewardGold, Get<Hero>(Server, scenario.PlayerA.HeroId).Gold);
            Assert.Equal(100, Get<Hero>(Server, scenario.PlayerB.HeroId).Gold);
            var giver = Get<Hero>(Server, scenario.NotableId);
            Assert.Equal(3, giver.GetRelation(Get<Hero>(Server, scenario.PlayerA.HeroId)));
            Assert.Equal(0, giver.GetRelation(Get<Hero>(Server, scenario.PlayerB.HeroId)));
        });
        AssertEndedEverywhere();
    }

    [Fact]
    public void RecruitmentFromTheOtherPlayersEncounterDoesNotCreditTheOwner()
    {
        Accept();
        var targetId = TargetIds().First();
        Recruit(targetId, scenario.PlayerB);
        Assert.Equal(0, Quest(Server)._recruitedPartyCount);
        Assert.Equal(0, Quest(ClientA)._questProgressLogTest.CurrentProgress);
        Assert.DoesNotContain(targetId, TargetIds());
    }

    [Fact]
    public void AuthoritativeEndingPreservesOwnerHuntProgress()
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);
        Accept();
        var serverQuest = Quest(Server);
        var ownerQuest = Quest(ClientA);
        Destroy(TargetIds().First(), scenario.PlayerA);
        ClientA.Call(() => Assert.Equal(1, ownerQuest._questProgressLogTest.CurrentProgress));

        Server.Call(() => Quest(Server).CompleteQuestWithCancel(), TextHelpers);
        var snapshot = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        ClientA.SimulateMessage(Server.NetPeer, Server.SerializeForWire(snapshot));
        ClientA.Call(() => Assert.Equal(1, ownerQuest._questProgressLogTest.CurrentProgress));
        environment.FlushCoalescer();
        Server.Call(() => Assert.Equal(1, Assert.Single(serverQuest.JournalEntries,
            log => log.Range == serverQuest._totalPartyCount).CurrentProgress));
        LogEntry[] saveHistory = null!;
        Server.Call(() => saveHistory = Campaign.Current.LogEntryHistory.GameActionLogs.ToArray());
        ClientA.Call(() =>
        {
            Campaign.Current.LogEntryHistory._logs.Clear();
            Campaign.Current.LogEntryHistory._logs.AddRange(saveHistory);
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId) && entry.IsEnded()));
            Assert.Equal(1, Assert.Single(archive.GetEntries(), log => log.Range == ownerQuest._totalPartyCount).CurrentProgress);
        });
    }

    [Fact]
    public void SuccessRequestWithoutEnoughServerProgressDoesNotPay()
    {
        Accept();
        ClientA.Call(() => Quest(ClientA).SuccessConsequences(), TextHelpers);
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Equal(100, Get<Hero>(Server, scenario.PlayerA.HeroId).Gold));
        Assert.True(Quest(ClientA).IsOngoing);
    }

    [Fact]
    public void FrameworkDisconnectDismissalReleasesTheServersTargets()
    {
        Accept();
        var serverQuest = Quest(Server);
        Assert.NotEmpty(serverQuest._validPartiesList);
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().ClearPeer(ClientA.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(ClientA.NetPeer, default(DisconnectInfo)));
        }, TextHelpers);
        Server.PumpGameThread();
        Assert.Empty(serverQuest._validPartiesList);
        Assert.True(serverQuest.IsFinalized);
        AssertEndedEverywhere();
    }

    [Fact]
    public void RaidOnTheGiversVillageCancelsItsQuest()
    {
        Accept();
        var villageId = environment.CreateRegisteredObject<Village>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Settlement>(instance, settlementId).SetSettlementComponent(Get<Village>(instance, villageId));
            });
        }
        Server.Call(() => CampaignEventDispatcher.Instance.OnVillageBeingRaided(Get<Village>(Server, villageId)), TextHelpers);
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertEndedEverywhere();
    }

    [Fact]
    public void TimeoutAppliesItsTownPenaltyOnceAndRetainsTheOwnersLog()
    {
        Accept();
        var before = 0f;
        Server.Call(() => before = Get<Town>(Server, townId).Prosperity);
        var ownerQuest = Quest(ClientA);
        ClientA.Call(() => ownerQuest.CompleteQuestWithTimeOut(), TextHelpers);
        Server.Call(() =>
        {
            Assert.Equal(before - 10f, Get<Town>(Server, townId).Prosperity);
            Assert.Equal(-5, Get<Hero>(Server, scenario.NotableId).GetRelation(Get<Hero>(Server, scenario.PlayerA.HeroId)));
        });
        Assert.Contains(ownerQuest.JournalEntries, log => log.LogText?.ToString().Contains("failed to defeat") == true);
        AssertEndedEverywhere();
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void ServerWarEndsTheOwnersQuest(DeclareWarAction.DeclareWarDetail detail, IssueOutcome expected)
    {
        Accept();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(instance, scenario.PlayerA.HeroId).Clan = Get<Clan>(instance, playerClanId);
            });
        }

        Server.Call(() => DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), Get<Clan>(Server, playerClanId), detail), TextHelpers);
        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertEndedEverywhere();
    }

    [Fact]
    public void AWarSeenOnTheOwnersClientLeavesTheQuestToTheServer()
    {
        Accept();
        var ownerQuest = Quest(ClientA);
        var logs = ownerQuest.JournalEntries.Count;
        ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                Get<Hero>(ClientA, scenario.PlayerA.HeroId).Clan = Get<Clan>(ClientA, playerClanId);
            }
            CampaignEventDispatcher.Instance.OnWarDeclared(Get<Kingdom>(ClientA, kingdomId), Get<Clan>(ClientA, playerClanId),
                DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
        }, TextHelpers);

        Assert.True(ownerQuest.IsOngoing);
        Assert.Equal(logs, ownerQuest.JournalEntries.Count);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void JoiningAKingdomAtWarWithTheGiverCancelsTheOwnersQuest()
    {
        Accept();
        var ownerQuest = Quest(ClientA);
        var enemyId = environment.CreateRegisteredObject<Kingdom>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(instance, scenario.PlayerA.HeroId).Clan = Get<Clan>(instance, playerClanId);
            });
        }

        Server.Call(() => DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), Get<Kingdom>(Server, enemyId),
            DeclareWarAction.DeclareWarDetail.Default), TextHelpers);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        Server.Call(() =>
        {
            var clan = Get<Clan>(Server, playerClanId);
            var enemy = Get<Kingdom>(Server, enemyId);
            using (new AllowedThread()) clan._kingdom = enemy;
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, null, enemy,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        }, TextHelpers);

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Contains(ownerQuest.JournalEntries, log => log.LogText?.ToString().Contains("now at war") == true);
        AssertEndedEverywhere();
    }

    [Fact]
    public void AKingdomChangeSeenOnTheOwnersClientLeavesTheQuestToTheServer()
    {
        Accept();
        var ownerQuest = Quest(ClientA);
        var logs = ownerQuest.JournalEntries.Count;
        var enemyId = environment.CreateRegisteredObject<Kingdom>();
        ClientA.Call(() =>
        {
            var clan = Get<Clan>(ClientA, playerClanId);
            var enemy = Get<Kingdom>(ClientA, enemyId);
            using (new AllowedThread())
            {
                Get<Hero>(ClientA, scenario.PlayerA.HeroId).Clan = clan;
                DeclareWarAction.ApplyInternal(Get<Kingdom>(ClientA, kingdomId), enemy,
                    DeclareWarAction.DeclareWarDetail.Default);
                clan._kingdom = enemy;
            }
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, null, enemy,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        }, TextHelpers);

        Assert.True(ownerQuest.IsOngoing);
        Assert.Equal(logs, ownerQuest.JournalEntries.Count);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void CoercingTheGiversVillageFailsOnlyTheCoercingOwnersQuest()
    {
        Accept();
        var otherGiverId = AcceptFromAnotherGiver();
        var ownerQuest = Quest(ClientA);
        var villageId = environment.CreateRegisteredObject<Village>();
        var boundId = environment.CreateRegisteredObject<Settlement>();
        var boundTownId = environment.CreateRegisteredObject<Town>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var bound = Get<Settlement>(instance, boundId);
                    bound.SetSettlementComponent(Get<Town>(instance, boundTownId));
                    Get<Town>(instance, boundTownId).OwnerClan = Get<Clan>(instance, giverClanId);
                    Get<Village>(instance, villageId)._bound = bound;
                    var settlement = Get<Settlement>(instance, settlementId);
                    settlement.SetSettlementComponent(Get<Village>(instance, villageId));
                    settlement.CollectNotablesToCache();
                }
            });
        }

        var power = 0f;
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, scenario.NotableId);
            power = giver.Power;
            var forcing = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
            forcing._mapEventType = MapEvent.BattleTypes.IsForcingSupplies;
            forcing.MapEventSettlement = Get<Settlement>(Server, settlementId);
            CampaignEventDispatcher.Instance.OnMapEventStarted(forcing, Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).Party, null);
        }, TextHelpers);

        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Contains(ownerQuest.JournalEntries, log => log.LogText?.ToString().Contains("no longer trusts you") == true);
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, scenario.NotableId);
            Assert.Equal(-5, giver.GetRelation(Get<Hero>(Server, scenario.PlayerA.HeroId)));
            Assert.Equal(0, giver.GetRelation(Get<Hero>(Server, scenario.PlayerB.HeroId)));
            Assert.Equal(power - 10f, giver.Power);
            Assert.True(Get<Hero>(Server, otherGiverId).Issue.IssueQuest.IsOngoing);
        });
        ClientB.Call(() => Assert.Contains(Get<Hero>(ClientB, otherGiverId).Issue.IssueQuest, Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void AnotherPlayerCanAcceptTheSameTypeFromAnotherGiver()
    {
        Accept();
        AcceptFromAnotherGiver();

        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.True(Quest(ClientA).IsOngoing);
    }

    [Fact]
    public void CommittedSelectionsAllowReturningWoundedTroopsWithXpUsingCanonicalPlayerIdentity()
    {
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            var troop = Get<CharacterObject>(Server, troopId);
            troop.UpgradeTargets = Array.Empty<CharacterObject>();
            var rosters = Server.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var broker = Server.Resolve<IMessageBroker>();
            var commitments = Server.Resolve<IAlternativeSolutionCommitments>();
            var empty = TroopRoster.CreateDummyTroopRoster();
            var selected = TroopRoster.CreateDummyTroopRoster();
            selected.AddToCounts(troop, 10, woundedCount: 2, xpChange: 100);
            var commit = rosters.PackTroopRosterDelta(selected, empty);
            Assert.True(commitments.CanApply(ClientA.NetPeer, scenario.NotableId, "return_test", commit));
            broker.Publish(this, new GameInterface.Services.Party.Messages.PartyDoneLogicApplied(
                ClientA.NetPeer, PartyScreenHelper.PartyScreenMode.QuestTroopManage, commit,
                "client_registry_alias", scenario.NotableId, "return_test"));
            var reduced = TroopRoster.CreateDummyTroopRoster();
            reduced.AddToCounts(troop, 8, woundedCount: 1, xpChange: 70);
            var returned = rosters.PackTroopRosterDelta(reduced, selected);
            Assert.True(commitments.CanApply(ClientA.NetPeer, scenario.NotableId, "return_test", returned));
            Assert.False(commitments.CanApply(ClientB.NetPeer, scenario.NotableId, "return_test", returned));
            broker.Publish(this, new GameInterface.Services.Party.Messages.PartyDoneLogicApplied(
                ClientA.NetPeer, PartyScreenHelper.PartyScreenMode.QuestTroopManage, returned,
                "client_registry_alias", scenario.NotableId, "return_test"));
            var remaining = Assert.Single(commitments.Consume(scenario.PlayerA.HeroId, scenario.NotableId, "return_test", null));
            Assert.Equal(8, remaining.Number);
            Assert.Equal(1, remaining.WoundedNumber);
            Assert.Equal(70, remaining.Xp);
        });
    }

    [Fact]
    public void CommittedSelectionsAreConsumedPerIssue()
    {
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            var rosters = Server.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var broker = Server.Resolve<IMessageBroker>();
            var commitments = Server.Resolve<IAlternativeSolutionCommitments>();
            var initial = TroopRoster.CreateDummyTroopRoster();
            initial.AddToCounts(Get<CharacterObject>(Server, troopId), 10);
            var delta = rosters.PackTroopRosterDelta(initial, TroopRoster.CreateDummyTroopRoster());
            foreach (var issueId in new[] { "pending_x", "accepted_y" })
                broker.Publish(this, new GameInterface.Services.Party.Messages.PartyDoneLogicApplied(
                    ClientA.NetPeer, PartyScreenHelper.PartyScreenMode.QuestTroopManage, delta,
                    "client_registry_alias", scenario.NotableId, issueId));
            Assert.Equal(10, Assert.Single(commitments.Consume(scenario.PlayerA.HeroId, scenario.NotableId, "accepted_y", null)).Number);
            Assert.Equal(10, Assert.Single(commitments.Consume(scenario.PlayerA.HeroId, scenario.NotableId, "pending_x", null)).Number);
            Assert.Empty(commitments.Consume(scenario.PlayerA.HeroId, scenario.NotableId, "pending_x", null));
        });
    }

    [Fact]
    public void FinalizingALoadedAlternativeRosterDoesNotGiveTheNonOwnerItsCompanion()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        ClientB.Call(() =>
        {
            var issue = Get<Hero>(ClientB, scenario.NotableId).Issue;
            var companion = Get<Hero>(ClientB, companionId).CharacterObject;
            var party = Get<MobileParty>(ClientB, scenario.PlayerB.MobilePartyId);
            using (new AllowedThread())
            {
                issue.AlternativeSolutionSentTroops.AddToCounts(companion, 1);
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.IssueFinalized();
            }
            Assert.Equal(0, party.MemberRoster.GetTroopCount(companion));
            Assert.Equal(0, issue.AlternativeSolutionSentTroops.TotalManCount);
        }, TextHelpers);
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, true, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, true, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, true, true, false, false, false, false, false, false)]
    [InlineData(false, true, true, true, true, false, false, false, false, false, false)]
    [InlineData(false, true, false, true, true, true, false, false, false, false, false)]
    [InlineData(false, true, false, true, true, false, true, false, false, false, false)]
    [InlineData(false, true, false, true, true, false, false, true, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, true, false, false)]
    [InlineData(false, true, false, true, true, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, false, false, false, true, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, true, false)]
    [InlineData(false, true, false, false, false, false, false, true, true, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, false, true)]
    [InlineData(false, false, false, false, false, false, false, false, false, false, true)]
    public void CompanionSolutionReturnsTroopsAndKeepsItsResultWithTheOwner(bool fails, bool cancel, bool cancelPicker, bool otherAccepts, bool otherDisconnects, bool acceptInFlight, bool otherAlternative, bool reopenPicker, bool rejectDone, bool disconnectSelecting, bool upgradePicker)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var unupgradedId = upgradePicker ? environment.CreateRegisteredObject<CharacterObject>() : null;
        var sent = 0;
        PartyScreenLogic pickerLogic = null!;
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Get<CharacterObject>(instance, troopId).Level = 20;
                    Get<CharacterObject>(instance, troopId).UpgradeTargets = Array.Empty<CharacterObject>();
                    Get<Hero>(instance, companionId).HitPoints = 100;
                    Get<Hero>(instance, companionId).SetSkillValue(DefaultSkills.Tactics, 150);
                    Get<Hero>(instance, companionId)._partyBelongedTo = Get<MobileParty>(instance, scenario.PlayerA.MobilePartyId);
                }
            });
        }
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId);
            sent = Get<Hero>(Server, scenario.NotableId).Issue.GetTotalAlternativeSolutionNeededMenCount();
            party.MemberRoster.AddToCounts(Get<Hero>(Server, companionId).CharacterObject, 1);
            party.MemberRoster.AddToCounts(Get<CharacterObject>(Server, troopId), upgradePicker ? 5 : sent + 5);
            if (upgradePicker) party.MemberRoster.AddToCounts(Get<CharacterObject>(Server, unupgradedId!), sent + 1);
        });
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            var party = Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId);
            var companion = Get<Hero>(ClientA, companionId);
            var troop = Get<CharacterObject>(ClientA, troopId);
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }

            ClientA.Resolve<IAlternativeSolutionPicker>().Open(issue);
            var logic = new PartyScreenLogic();
            pickerLogic = logic;
            logic.Initialize(PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                issue.AlternativeSolutionSentTroops, TroopRoster.CreateDummyTroopRoster(),
                PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable,
                PartyScreenLogic.TransferState.Transferable, null, PartyScreenHelper.PartyScreenMode.QuestTroopManage,
                partyPresentationDoneButtonDelegate: PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler));
            logic._partyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
            if (upgradePicker)
            {
                // Stage the roster effects of upgrading in the open picker, keeping its original snapshots.
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(Get<CharacterObject>(ClientA, unupgradedId!), -(sent + 1));
                    party.MemberRoster.AddToCounts(troop, sent + 1);
                }
            }
            var transfer = new PartyScreenLogic.PartyCommand();
            transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, sent, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(transfer, false);
            if (cancelPicker && !otherDisconnects)
            {
                using (new AllowedThread()) logic.Reset(true);
            }
            else if (!cancelPicker)
            {
                if (rejectDone) ClientA.Resolve<TestNetworkRouter>().PauseLink(ClientA.NetPeer, Server.NetPeer);
                Assert.True(logic.DoneLogic(false));
                Assert.Equal(sent, issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
            }
            if (cancelPicker && otherDisconnects)
            {
                if (Game.Current.GameStateManager._gameStates.Count == 0)
                    Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.MapState>());
                var state = Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.PartyState>();
                state.PartyScreenLogic = pickerLogic;
                state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
                Game.Current.GameStateManager._gameStates.Add(state);
                Assert.Same(state, Game.Current.GameStateManager.ActiveState);
            }
        }, TextHelpers);
        environment.FlushCoalescer();

        if (rejectDone)
        {
            Server.Call(() => Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster
                .AddToCounts(Get<CharacterObject>(Server, troopId), -6));
            var delayed = ClientA.Resolve<TestNetworkRouter>();
            delayed.ResumeLink(ClientA.NetPeer, Server.NetPeer);
            delayed.DrainReady();
            environment.FlushCoalescer();
            Server.Call(() => Assert.Equal(sent - 1,
                Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId))));
        }
        var expectedRegulars = rejectDone ? sent - 1 : sent + (upgradePicker ? 6 : 5);
        if (disconnectSelecting)
        {
            Server.Call(() =>
            {
                Server.Resolve<IPlayerManager>().ClearPeer(ClientA.NetPeer);
                Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(ClientA.NetPeer, default(DisconnectInfo)));
            });
            Server.Call(() => Assert.Equal(expectedRegulars,
                Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId))));
            Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this,
                new PlayerDisconnected(ClientA.NetPeer, default(DisconnectInfo))));
            Server.Call(() => Assert.Equal(expectedRegulars,
                Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId))));
            return;
        }

        if (reopenPicker)
        {
            string? replacementId = null;
            if (rejectDone)
            {
                replacementId = environment.CreateRegisteredObject<CharacterObject>();
                foreach (var instance in Instances)
                    instance.Call(() =>
                    {
                        using (new AllowedThread())
                        {
                            Get<CharacterObject>(instance, replacementId).Level = 20;
                            Get<CharacterObject>(instance, replacementId).UpgradeTargets = Array.Empty<CharacterObject>();
                        }
                    });
                Server.Call(() => Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster
                    .AddToCounts(Get<CharacterObject>(Server, replacementId), 1));
                environment.FlushCoalescer();
            }
            ClientA.Call(() =>
            {
                var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
                var party = Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId);
                ClientA.Resolve<IAlternativeSolutionPicker>().Open(issue);
                pickerLogic = new PartyScreenLogic();
                pickerLogic.Initialize(new PartyScreenLogicInitializationData
                {
                    LeftMemberRoster = issue.AlternativeSolutionSentTroops,
                    RightMemberRoster = party.MemberRoster,
                    LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                    RightPrisonerRoster = party.PrisonRoster,
                    RightOwnerParty = party.Party,
                    RightLeaderHero = Get<Hero>(ClientA, scenario.PlayerA.HeroId),
                    PartyPresentationDoneButtonDelegate = PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler,
                });
                pickerLogic._partyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
                var transfer = new PartyScreenLogic.PartyCommand();
                if (rejectDone)
                {
                    transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left,
                        PartyScreenLogic.TroopType.Member, Get<CharacterObject>(ClientA, troopId), 1, 0, -1);
                    using (new AllowedThread()) pickerLogic.TransferTroop(transfer, false);
                    transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                        PartyScreenLogic.TroopType.Member, Get<CharacterObject>(ClientA, replacementId!), 1, 0, -1);
                    using (new AllowedThread()) pickerLogic.TransferTroop(transfer, false);
                    Assert.True(pickerLogic.DoneLogic(false));
                    return;
                }
                transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Member, Get<CharacterObject>(ClientA, troopId), 1, 0, -1);
                using (new AllowedThread()) pickerLogic.TransferTroop(transfer, false);
                Assert.Equal(sent + 1, issue.AlternativeSolutionSentTroops.GetTroopCount(Get<CharacterObject>(ClientA, troopId)));
                if (otherDisconnects)
                {
                    if (Game.Current.GameStateManager._gameStates.Count == 0)
                        Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.MapState>());
                    var state = Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.PartyState>();
                    state.PartyScreenLogic = pickerLogic;
                    state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
                    Game.Current.GameStateManager._gameStates.Add(state);
                    Assert.Same(state, Game.Current.GameStateManager.ActiveState);
                }
            }, TextHelpers);
            if (rejectDone)
            {
                Server.Call(() =>
                {
                    var party = Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId);
                    Assert.Equal(expectedRegulars, party.MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId)));
                    Assert.Equal(1, party.MemberRoster.GetTroopCount(Get<CharacterObject>(Server, replacementId!)));
                });
            }
        }

        if (otherAlternative)
        {
            var otherCompanionId = environment.CreateRegisteredObject<Hero>();
            foreach (var instance in Instances)
            {
                instance.Call(() =>
                {
                    using (new AllowedThread())
                    {
                        var hero = Get<Hero>(instance, otherCompanionId);
                        hero.HitPoints = 100;
                        hero.SetSkillValue(DefaultSkills.Tactics, 150);
                        hero._partyBelongedTo = Get<MobileParty>(instance, scenario.PlayerB.MobilePartyId);
                    }
                });
            }
            Server.Call(() =>
            {
                var party = Get<MobileParty>(Server, scenario.PlayerB.MobilePartyId);
                party.MemberRoster.AddToCounts(Get<Hero>(Server, otherCompanionId).CharacterObject, 1);
                party.MemberRoster.AddToCounts(Get<CharacterObject>(Server, troopId), sent + 5);
            });
            environment.FlushCoalescer();
            ClientB.Call(() =>
            {
                var issue = Get<Hero>(ClientB, scenario.NotableId).Issue;
                var party = Get<MobileParty>(ClientB, scenario.PlayerB.MobilePartyId);
                var companion = Get<Hero>(ClientB, otherCompanionId).CharacterObject;
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(companion, -1);
                    issue.AlternativeSolutionSentTroops.AddToCounts(companion, 1);
                }
                ClientB.Resolve<IAlternativeSolutionPicker>().Open(issue);
                var logic = new PartyScreenLogic();
                logic.Initialize(new PartyScreenLogicInitializationData
                {
                    LeftMemberRoster = issue.AlternativeSolutionSentTroops,
                    RightMemberRoster = party.MemberRoster,
                    LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                    RightPrisonerRoster = party.PrisonRoster,
                    RightOwnerParty = party.Party,
                    RightLeaderHero = Get<Hero>(ClientB, scenario.PlayerB.HeroId),
                    PartyPresentationDoneButtonDelegate = PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler,
                });
                logic._partyScreenMode = PartyScreenHelper.PartyScreenMode.QuestTroopManage;
                var transfer = new PartyScreenLogic.PartyCommand();
                transfer.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Member, Get<CharacterObject>(ClientB, troopId), sent, 0, -1);
                using (new AllowedThread()) logic.TransferTroop(transfer, false);
                Assert.True(logic.DoneLogic(false));
            }, TextHelpers);
            environment.FlushCoalescer();
            ClientB.Call(() =>
            {
                var issue = Get<Hero>(ClientB, scenario.NotableId).Issue;
                issue.StartIssueWithAlternativeSolution();
            }, TextHelpers);
            Assert.Equal(QuestScenario.ControllerB, scenario.OwnerOnServer());
            ClientA.Call(() => Assert.True(Get<Hero>(ClientA, scenario.NotableId).Issue.IsSolvingWithAlternative));
        }
        else if (otherAccepts) scenario.Accept(ClientB);

        if (otherDisconnects)
        {
            var router = ClientA.Resolve<TestNetworkRouter>();
            if (acceptInFlight)
            {
                router.PauseLink(ClientA.NetPeer, Server.NetPeer);
                ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithAlternativeSolution(), TextHelpers);
            }
            Server.Call(() =>
            {
                if (otherAlternative)
                {
                    var owner = Get<Hero>(Server, scenario.PlayerB.HeroId);
                    using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                    {
                        var issue = Get<Hero>(Server, scenario.NotableId).Issue;
                        issue._failureChance = -1f;
                        issue.CompleteIssueWithAlternativeSolution();
                    }
                }
                else
                {
                    Server.Resolve<IPlayerManager>().ClearPeer(ClientB.NetPeer);
                    Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(ClientB.NetPeer, default(DisconnectInfo)));
                }
            }, TextHelpers);
            environment.FlushCoalescer();
            if (acceptInFlight)
            {
                router.ResumeLink(ClientA.NetPeer, Server.NetPeer);
                router.DrainReady();
                environment.FlushCoalescer();
            }
            Server.Call(() => Assert.Equal(expectedRegulars,
                Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId))));
            ClientA.Call(() =>
            {
                if (cancelPicker || reopenPicker)
                {
                    Assert.False(Game.Current.GameStateManager.ActiveState is TaleWorlds.CampaignSystem.GameState.PartyState);
                }
                Assert.Null(Get<Hero>(ClientA, scenario.NotableId).Issue);
                Assert.Equal(1, Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<Hero>(ClientA, companionId).CharacterObject));
                Assert.Equal(expectedRegulars, Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(ClientA, troopId)));
            });
            return;
        }

        ClientA.Call(() =>
        {
            var agent = new Mock<IAgent>();
            agent.SetupGet(value => value.Character).Returns(Get<Hero>(ClientA, scenario.NotableId).CharacterObject);
            Campaign.Current.ConversationManager._conversationAgents.Add(agent.Object);
            if (cancel) IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence();
            else IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence();
        }, TextHelpers);
        environment.FlushCoalescer();

        if (cancel)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
            Server.Call(() =>
            {
                Assert.Equal(expectedRegulars, Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId)));
                Assert.Equal(!otherAccepts, Get<Hero>(Server, scenario.NotableId).Issue.IsOngoingWithoutQuest);
            });
            if (otherAccepts) Assert.Equal(QuestScenario.ControllerB, scenario.OwnerOnServer());
            return;
        }

        if (rejectDone)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
            Server.Call(() => Assert.Equal(expectedRegulars,
                Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId))));
            ClientA.Call(() =>
            {
                Assert.Equal(1, Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<Hero>(ClientA, companionId).CharacterObject));
                Assert.Equal(expectedRegulars, Get<MobileParty>(ClientA, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(ClientA, troopId)));
            });
            return;
        }
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Issue ownerIssue = null!;
        ClientA.Call(() => ownerIssue = Assert.IsType<Issue>(Get<Hero>(ClientA, scenario.NotableId).Issue));
        foreach (var instance in Instances)
        {
            instance.Call(() => Get<Hero>(instance, scenario.NotableId).Issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Now);
        }
        Server.Call(() => Campaign.Current.IssueManager.DailyTick(), TextHelpers);
        Assert.Equal(2, ownerIssue.JournalEntries.Count);
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkMerchantOutlawsProgress>());
        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, scenario.NotableId).Issue;
            issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.DaysFromNow(1);
            issue.JournalEntries[0].UpdateCurrentProgress(issue.JournalEntries[0].Range);
            Campaign.Current.IssueManager.DailyTick();
        }, TextHelpers);
        var unchangedDayCount = Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count();
        Server.Call(() =>
        {
            for (var day = 0; day < 200; day++) Campaign.Current.IssueManager.DailyTick();
        }, TextHelpers);
        Assert.Equal(unchangedDayCount, Server.NetworkSentMessages.GetMessages<NetworkMerchantOutlawsProgress>().Count());

        foreach (var client in new[] { ClientA, ClientB })
        {
            client.Call(() => Campaign.Current.IssueManager.DailyTick(), TextHelpers);
        }
        Assert.Equal(2, ownerIssue.JournalEntries.Count);

        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, scenario.NotableId).Issue;
            issue._failureChance = fails ? 1f : -1f;
            issue._alternativeSolutionCasualtyCount = 1;
            issue.CompleteIssueWithAlternativeSolution();
        }, TextHelpers);
        environment.FlushCoalescer();

        Server.Call(() =>
        {
            Assert.Equal(100 + (fails ? 0 : 550), Get<Hero>(Server, scenario.PlayerA.HeroId).Gold);
            Assert.Equal(100, Get<Hero>(Server, scenario.PlayerB.HeroId).Gold);
            Assert.Equal(sent + (upgradePicker ? 5 : 4), Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<CharacterObject>(Server, troopId)));
            Assert.Equal(fails ? 490f : 505f, Get<Town>(Server, townId).Prosperity);
            Assert.Equal(1, Get<MobileParty>(Server, scenario.PlayerA.MobilePartyId).MemberRoster.GetTroopCount(Get<Hero>(Server, companionId).CharacterObject));
        });
        Assert.Single(ownerIssue.JournalEntries.Where(log => log.LogText?.ToString().Contains(fails ? "failed to resolve" : "had resolved") == true));
        AssertEndedEverywhere();
    }

    [Fact]
    public void ReceivedServerProgressDoesNotEchoAnOwnerSnapshot()
    {
        Accept();
        var total = 0;
        ClientA.Call(() => total = Quest(ClientA)._totalPartyCount);
        Assert.True(total > 0);
        var before = ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Count();
        ClientA.SimulateMessage(Server.NetPeer, new NetworkMerchantOutlawsProgress(scenario.NotableId,
            QuestScenario.IssueId, QuestScenario.ControllerA, total, 0, Array.Empty<string>(), Array.Empty<string>()));
        environment.FlushCoalescer();
        ClientA.Call(() => Assert.Equal(total, Quest(ClientA)._questProgressLogTest.CurrentProgress));
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Skip(before));
    }

    private void Accept(bool startQuest = true)
    {
        ClientA.Call(() =>
        {
            var player = Get<Hero>(ClientA, scenario.PlayerA.HeroId);
            using (new MainHeroSubstitutionScope(player, player.PartyBelongedTo))
            {
                Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithQuest();
                if (startQuest) Quest(ClientA).QuestAcceptedConsequences();
            }
        }, TextHelpers);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    private string AcceptFromAnotherGiver()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            using (new AllowedThread())
            {
                giver.StayingInSettlement = Get<Settlement>(Server, settlementId);
                giver.Occupation = Occupation.Merchant;
                giver.Clan = Get<Clan>(Server, giverClanId);
            }
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) =>
                new Issue(owner, Get<Hideout>(Server, hideoutId)), typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        foreach (var client in new[] { ClientA, ClientB })
        {
            client.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(client, giverId);
                    giver.StayingInSettlement = Get<Settlement>(client, settlementId);
                    giver.Clan = Get<Clan>(client, giverClanId);
                }
            });
        }
        ClientB.Call(() =>
        {
            var player = Get<Hero>(ClientB, scenario.PlayerB.HeroId);
            using (new MainHeroSubstitutionScope(player, player.PartyBelongedTo))
            {
                var issue = Get<Hero>(ClientB, giverId).Issue;
                issue.StartIssueWithQuest();
                Assert.IsType<Quest>(issue.IssueQuest).QuestAcceptedConsequences();
            }
        }, TextHelpers);
        return giverId;
    }

    private string[] TargetIds()
    {
        var ids = Array.Empty<string>();
        Server.Call(() => ids = Quest(Server)._validPartiesList.Select(party =>
        {
            Assert.True(Server.ObjectManager.TryGetId(party, out var id));
            return id;
        }).ToArray());
        return ids;
    }

    private void Destroy(string targetId, Player player)
    {
        Server.Call(() => CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(
            Get<MobileParty>(Server, targetId), Get<MobileParty>(Server, player.MobilePartyId).Party), TextHelpers);
    }

    private void Recruit(string targetId, Player player)
    {
        Server.SimulateMessage(this, new NetworkRosterScreenAfterBanditEncounter(new List<string> { targetId }, player.MobilePartyId));
    }

    private Quest Quest(EnvironmentInstance instance) => Assert.IsType<Quest>(Get<Hero>(instance, scenario.NotableId).Issue.IssueQuest);

    private static T Get<T>(EnvironmentInstance instance, string id)
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private void AssertEndedEverywhere()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() => Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue));
        }
    }
}
