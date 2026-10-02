using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

public sealed class LordWantsRivalCapturedTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Client => environment.Clients.First();
    private EnvironmentInstance OtherClient => environment.Clients.Last();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(Server);

    private readonly string giverId;
    private readonly string targetId;
    private readonly string playerId;
    private readonly string partyId;
    private readonly string giverPartyId;
    private const string Controller = "rival-captured-player";

    public LordWantsRivalCapturedTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        giverId = environment.CreateRegisteredObject<Hero>();
        targetId = environment.CreateRegisteredObject<Hero>();
        playerId = environment.CreateRegisteredObject<Hero>();
        partyId = environment.CreateRegisteredObject<MobileParty>();
        giverPartyId = environment.CreateRegisteredObject<MobileParty>();
        var targetPartyId = environment.CreateRegisteredObject<MobileParty>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var kingdomId = environment.CreateRegisteredObject<Kingdom>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var giver = Get<Hero>(instance, giverId);
                var target = Get<Hero>(instance, targetId);
                var player = Get<Hero>(instance, playerId);
                var party = Get<MobileParty>(instance, partyId);
                var giverParty = Get<MobileParty>(instance, giverPartyId);
                var targetParty = Get<MobileParty>(instance, targetPartyId);
                var kingdom = Get<Kingdom>(instance, kingdomId);
                var settlement = Get<Settlement>(instance, settlementId);

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                    giver.Clan.SetLeader(giver);
                    target.Clan.SetLeader(target);
                    player.Clan.SetLeader(player);
                    giver.Clan._kingdom = kingdom;
                    player.Clan._kingdom = kingdom;
                    player.Clan._tier = 2;
                    giver.PartyBelongedTo = giverParty;
                    target.PartyBelongedTo = targetParty;
                    player.PartyBelongedTo = party;
                    giverParty.ActualClan = giver.Clan;
                    party.ActualClan = player.Clan;
                    targetParty.ActualClan = target.Clan;
                    party.CurrentSettlement = settlement;
                    giver.StayingInSettlement = settlement;
                    party.MemberRoster.AddToCounts(Get<CharacterObject>(instance, troopId), 50);
                    targetParty.MemberRoster.AddToCounts(target.CharacterObject, 1);
                    if (instance == Client)
                    {
                        Game.Current.PlayerTroop = player.CharacterObject;
                        Campaign.Current.MainParty = party;
                        Campaign.Current.PlayerDefaultFaction = player.Clan;
                    }
                }

                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(Controller, playerId, partyId, "", "")));
            });
        }

        environment.ConnectRegisteredPlayer(Client, Controller);
        Client.Resolve<IControllerIdProvider>().SetControllerId(Controller);
        OtherClient.Resolve<IControllerIdProvider>().SetControllerId("other-rival-player");
        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            var data = new PotentialIssueData((in PotentialIssueData _, Hero giver) => new Issue(giver, target),
                typeof(Issue), IssueBase.IssueFrequency.Rare);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in data, Get<Hero>(Server, giverId)));
        });
    }

    public void Dispose() => environment.Dispose();

    private static T Get<T>(EnvironmentInstance instance, string id)
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private Quest GetQuest(EnvironmentInstance instance) => Assert.IsType<Quest>(Get<Hero>(instance, giverId).Issue.IssueQuest);

    private void Accept()
    {
        Client.Call(() =>
        {
            var giver = Get<Hero>(Client, giverId);
            using (new MainHeroSubstitutionScope(Get<Hero>(Client, playerId), Get<MobileParty>(Client, partyId)))
            {
                MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, Controller));
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
            }
        });
        Server.Call(() => Assert.True(GetQuest(Server).IsOngoing));
    }

    [Fact]
    public void CreationPreservesTheSelectedTargetAndIssueIdentityOnBothClients()
    {
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRivalCapturedIssueCreated>());
        Assert.Equal(targetId, created.TargetId);
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                var issue = Assert.IsType<Issue>(Get<Hero>(client, giverId).Issue);
                Assert.Same(Get<Hero>(client, targetId), issue._targetHero);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
            });
        }
    }

    [Fact]
    public void AcceptAndProgressAppearOnlyInTheAcceptingPlayersJournal()
    {
        Accept();
        Server.Call(() => GetQuest(Server).AddLog(new TextObject("A captured rival update")));
        Client.Call(() =>
        {
            var quest = GetQuest(Client);
            Assert.True(quest.IsOngoing);
            Assert.Equal("A captured rival update", quest.JournalEntries.Last().LogText.ToString());
            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
        });
        OtherClient.Call(() =>
        {
            var issue = Get<Hero>(OtherClient, giverId).Issue;
            Assert.True(issue.IsSolvingWithQuest);
            Assert.Null(issue.IssueQuest);
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
        });
    }

    [Fact]
    public void ClientWorldCallbackDoesNotRewardEvenDuringAReceivedWorldUpdate()
    {
        Accept();
        Client.Call(() =>
        {
            var player = Get<Hero>(Client, playerId);
            var quest = GetQuest(Client);
            var gold = player.Gold;
            using (new MainHeroSubstitutionScope(player, Get<MobileParty>(Client, partyId)))
            using (new AllowedThread())
                quest.OnHeroKilled(Get<Hero>(Client, targetId), player, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
            Assert.True(quest.IsOngoing);
            Assert.Equal(gold, player.Gold);
        });
    }

    [Fact]
    public void StaleProgressCannotOverwriteTheOwnersJournal()
    {
        Accept();
        Client.SimulateMessage(Server.NetPeer, new NetworkRivalCapturedProgress(giverId, 0,
            new RivalCapturedQuestState(true, 15, Array.Empty<RivalCapturedLog>())));
        Client.Call(() =>
        {
            var quest = GetQuest(Client);
            Assert.Single(quest.JournalEntries);
            Assert.False(quest._firstCounterOfferMade);
            Assert.Equal(0, quest.RelationshipChangeWithQuestGiver);
        });
    }

    [Fact]
    public void FirstCounterOfferCapturesThePendingHeroOnceThroughTheServerAction()
    {
        Accept();
        Server.Call(() => Assert.False(Get<Hero>(Server, targetId).IsPrisoner));
        Client.Call(() => GetQuest(Client).FirstCounterOfferFinished());
        Client.Call(() => GetQuest(Client).FirstCounterOfferFinished());
        environment.FlushCoalescer();
        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            var party = Get<MobileParty>(Server, partyId);
            Assert.True(target.IsPrisoner);
            Assert.Same(party.Party, target.PartyBelongedToAsPrisoner);
            Assert.Equal(1, party.PrisonRoster.GetTroopCount(target.CharacterObject));
            Assert.Equal(4, GetQuest(Server).JournalEntries.Count);
        });
        Client.Call(() =>
        {
            Assert.True(GetQuest(Client)._firstCounterOfferMade);
            Assert.Equal(4, GetQuest(Client).JournalEntries.Count);
        });
        OtherClient.Call(() => Assert.Null(Get<Hero>(OtherClient, giverId).Issue.IssueQuest));
    }

    [Fact]
    public void RepeatedCounterOfferRequestsDoNotCaptureTheSamePrisonerAgain()
    {
        Accept();
        var generation = 0;
        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            var party = Get<MobileParty>(Server, partyId);
            using (new AllowedThread())
            {
                target.PartyBelongedToAsPrisoner = party.Party;
                party.PrisonRoster.AddToCounts(target.CharacterObject, 1);
            }
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(Get<Hero>(Server, giverId), out generation));
        });
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedChoice(giverId, generation - 1, RivalCapturedChoice.HearCounterOffer));
        Server.Call(() => Assert.False(GetQuest(Server)._firstCounterOfferMade));
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedChoice(giverId, generation, RivalCapturedChoice.HearCounterOffer));
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedChoice(giverId, generation, RivalCapturedChoice.HearCounterOffer));
        Server.Call(() =>
        {
            Assert.True(GetQuest(Server)._firstCounterOfferMade);
            Assert.Equal(1, Get<MobileParty>(Server, partyId).PrisonRoster.GetTroopCount(Get<Hero>(Server, targetId).CharacterObject));
        });
        Client.Call(() => Assert.True(GetQuest(Client)._firstCounterOfferMade));
    }

    [Fact]
    public void ServerTimeoutKeepsTheFinalJournalAndFinalizesOnceOnEachPeer()
    {
        Accept();
        Quest clientQuest = null;
        Client.Call(() => clientQuest = GetQuest(Client));
        Server.Call(() =>
        {
            var quest = GetQuest(Server);
            quest.CompleteQuestWithTimeOut();
            quest.CompleteQuestWithTimeOut();
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Client.Call(() =>
        {
            Assert.True(clientQuest.IsFinalized);
            Assert.Equal(2, clientQuest.JournalEntries.Count);
            Assert.Equal(-5, clientQuest.RelationshipChangeWithQuestGiver);
            Assert.Null(Get<Hero>(Client, giverId).Issue);
            Assert.DoesNotContain(clientQuest, Campaign.Current.QuestManager.Quests);
        });
        OtherClient.Call(() => Assert.Null(Get<Hero>(OtherClient, giverId).Issue));
    }

    [Fact]
    public void TheSamePlayerCannotTakeAnotherRivalQuestButAnotherPlayerCan()
    {
        Accept();
        Server.Call(() =>
        {
            var otherIssue = new Issue(Get<Hero>(Server, targetId), Get<Hero>(Server, giverId));
            var service = Server.Resolve<ILordWantsRivalCapturedQuestService>();
            using (new MainHeroSubstitutionScope(Get<Hero>(Server, playerId), Get<MobileParty>(Server, partyId)))
                Assert.True(service.HasOtherPersonalQuest(otherIssue));
        });
        OtherClient.Call(() => Assert.False(OtherClient.Resolve<ILordWantsRivalCapturedQuestService>()
            .HasOtherPersonalQuest(new Issue(Get<Hero>(OtherClient, targetId), Get<Hero>(OtherClient, giverId)))));
    }

    [Fact]
    public void FailedPlayerReplacementKeepsTheQuestAndSuccessfulReplacementCancelsIt()
    {
        Accept();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer(Controller, out var registered));
            var replacement = new Player(Controller, targetId, partyId, "", "");
            Assert.False(players.ReplacePlayer(new Player(Controller, playerId, partyId, "", ""), replacement));
            Assert.True(GetQuest(Server).IsOngoing);
            Assert.True(players.ReplacePlayer(registered, replacement));
            Assert.Null(Get<Hero>(Server, giverId).Issue);
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Client.Call(() => Assert.Null(Get<Hero>(Client, giverId).Issue));
    }

    [Fact]
    public void PlayerChangeCallbackPreservesOwnedQuestAndRestoresSavedHonorXp()
    {
        Accept();
        Client.Call(() =>
        {
            var player = Get<Hero>(Client, playerId);
            var party = Get<MobileParty>(Client, partyId);
            var quest = GetQuest(Client);
            var progress = new PropertyOwner<PropertyObject>();
            progress.SetPropertyValue(DefaultTraits.Honor, -37);
            GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(player, progress);
            Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();

            // Loading allows originals; this callback follows Campaign's trait-store reset.
            using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
                Campaign.Current.QuestManager.OnPlayerCharacterChanged(player, player, party, false);

            Assert.Same(quest, GetQuest(Client));
            Assert.True(quest.IsOngoing);
            Assert.Single(quest.JournalEntries);
            Assert.True(quest.IsTracked(Get<Hero>(Client, targetId)));
            Assert.True(Client.Resolve<IIssueOwnershipRegistry>().IsLocalPeerOwner(quest.QuestGiver));
            Assert.Equal(-37, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        });
        Server.Call(() => Assert.True(GetQuest(Server).IsOngoing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermanentRemovalCancelsOwnedQuestButRejectedRemovalDoesNot(bool ownerObjectsGone)
    {
        Accept();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer(Controller, out var registered));
            Assert.False(players.RemovePlayer(new Player(Controller, playerId, partyId, "", "")));
            Assert.True(GetQuest(Server).IsOngoing);
            if (ownerObjectsGone)
            {
                Assert.True(Server.ObjectManager.Remove(Get<Hero>(Server, playerId)));
                Assert.True(Server.ObjectManager.Remove(Get<MobileParty>(Server, partyId)));
            }
            Assert.True(players.RemovePlayer(registered));
            Assert.Null(Get<Hero>(Server, giverId).Issue);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(Get<Hero>(Server, giverId), out _));
            Assert.True(Server.ObjectManager.TryGetId(Get<Hero>(Server, targetId).PartyBelongedTo, out var replacementPartyId));
            Assert.True(players.AddPlayer(new Player(Controller, targetId, replacementPartyId, "", "")));
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Client.Call(() => Assert.Null(Get<Hero>(Client, giverId).Issue));
        OtherClient.Call(() => Assert.Null(Get<Hero>(OtherClient, giverId).Issue));
    }

    [Fact]
    public void MovingGiverRequiresTheRequestersExistingPartyEngagement()
    {
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            using (new AllowedThread()) giver.StayingInSettlement = null;
            var service = Server.Resolve<ILordWantsRivalCapturedQuestService>();
            Assert.False(service.IsPresentWithGiver(Controller, giver));
            Assert.True(Server.ObjectManager.TryGetId(giver.PartyBelongedTo.Party, out var targetPartyId));
            Assert.True(Server.ObjectManager.TryGetId(Get<MobileParty>(Server, partyId).Party, out var playerPartyId));
            Assert.True(Server.Resolve<ConversationPartyTracker>().TryBeginEngagement(
                Client.NetPeer, playerPartyId, targetPartyId, false));
            Assert.True(service.IsPresentWithGiver(Controller, giver));
        });
    }

    [Fact]
    public void OwnerScopeKeepsTraitProgressSeparateFromTheCampaignAndOtherClient()
    {
        Accept();
        var otherXp = 0;
        OtherClient.Call(() => otherXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        Server.Call(() =>
        {
            var originalHero = Hero.MainHero;
            var originalParty = MobileParty.MainParty;
            var shared = Campaign.Current.PlayerTraitDeveloper;
            var sharedXp = shared.GetPropertyValue(DefaultTraits.Honor);
            Assert.True(Server.Resolve<ILordWantsRivalCapturedQuestService>().TryEnterOwnerScope(GetQuest(Server), out var scope));
            using (scope)
            {
                Assert.Same(Get<Hero>(Server, playerId), Hero.MainHero);
                Assert.Same(Get<MobileParty>(Server, partyId), MobileParty.MainParty);
                Assert.NotSame(shared, Campaign.Current.PlayerTraitDeveloper);
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, -50);
            }
            Assert.Same(originalHero, Hero.MainHero);
            Assert.Same(originalParty, MobileParty.MainParty);
            Assert.Same(shared, Campaign.Current.PlayerTraitDeveloper);
            Assert.Equal(sharedXp, shared.GetPropertyValue(DefaultTraits.Honor));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRivalCapturedTraitProgress>());
        Client.Call(() => Assert.Equal(-50, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        OtherClient.Call(() => Assert.Equal(otherXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
    }

    [Fact]
    public void HonorChangesPreserveTheOwnersExistingXpAndRestoreTheLatestReceivedValue()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(instance, playerId).SetTraitLevel(DefaultTraits.Honor, 1);
                var saved = new PropertyOwner<PropertyObject>();
                saved.SetPropertyValue(DefaultTraits.Calculating, 37);
                GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(Get<Hero>(instance, playerId), saved);
            });
        }
        Client.Call(() => Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 1500));
        var otherXp = 0;
        OtherClient.Call(() => otherXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        Accept();

        Server.Call(() =>
        {
            var shared = Campaign.Current.PlayerTraitDeveloper;
            var sharedXp = shared.GetPropertyValue(DefaultTraits.Honor);
            Assert.True(Server.Resolve<ILordWantsRivalCapturedQuestService>().TryEnterOwnerScope(GetQuest(Server), out var scope));
            using (scope)
            {
                Assert.Equal(1500, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
                TraitLevelingHelper.OnIssueFailed(Get<Hero>(Server, giverId),
                    new[] { Tuple.Create(DefaultTraits.Honor, -50) });
            }
            Assert.Same(shared, Campaign.Current.PlayerTraitDeveloper);
            Assert.Equal(sharedXp, shared.GetPropertyValue(DefaultTraits.Honor));
        });
        Client.Call(() =>
        {
            Assert.Equal(1450, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            TraitLevelingHelper.OnIssueSolvedThroughQuest(Get<Hero>(Client, giverId), DefaultTraits.Honor, 20);
        });
        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                    .TryGet(Get<Hero>(instance, playerId), out var saved));
                Assert.Equal(1470, saved.GetPropertyValue(DefaultTraits.Honor));
                Assert.Equal(37, saved.GetPropertyValue(DefaultTraits.Calculating));
            });
        }
        Client.Call(() =>
        {
            var player = Get<Hero>(Client, playerId);
            Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
            using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
                Campaign.Current.QuestManager.OnPlayerCharacterChanged(player, player, Get<MobileParty>(Client, partyId), false);
            Assert.Equal(1470, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.True(GetQuest(Client).IsOngoing);
        });
        OtherClient.Call(() => Assert.Equal(otherXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
    }

    [Fact]
    public void HonorBaselineRejectsStaleGenerationsAndQueuedDeltasKeepTheirPlayerIdentityAfterCompletion()
    {
        var generation = 0;
        Server.Call(() => Assert.True(Server.Resolve<IIssueGenerationRegistry>()
            .TryGetGeneration(Get<Hero>(Server, giverId), out generation)));
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedTraitProgress(giverId, generation - 1, 900, true, playerId));
        Server.Call(() => Assert.False(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
            .TryGet(Get<Hero>(Server, playerId), out _)));
        Accept();
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedTraitProgress(giverId, generation, 900, true, playerId));
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedTraitProgress(giverId, generation, 900, false, targetId));
        Server.SimulateMessage(OtherClient.NetPeer, new RequestRivalCapturedTraitProgress(giverId, generation, 900, false, playerId));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var saved));
            Assert.Equal(0, saved.GetPropertyValue(DefaultTraits.Honor));
            GetQuest(Server).CompleteQuestWithTimeOut();
        });
        Server.SimulateMessage(Client.NetPeer, new RequestRivalCapturedTraitProgress(giverId, generation, 20, false, playerId));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var saved));
            Assert.Equal(20, saved.GetPropertyValue(DefaultTraits.Honor));
        });
        Client.Call(() => Assert.Equal(20, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        Client.Call(() => TraitLevelingHelper.OnIssueSolvedThroughQuest(Get<Hero>(Client, giverId), DefaultTraits.Honor, 20));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var saved));
            Assert.Equal(40, saved.GetPropertyValue(DefaultTraits.Honor));
        });
    }

    [Fact]
    public void AcceptanceBaselineCannotOverwriteEstablishedZeroHonorProgress()
    {
        Client.Call(() => Client.Resolve<ILordWantsRivalCapturedQuestService>().PrepareAcceptance(Get<Hero>(Client, giverId)));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var saved));
            Assert.True(saved.HasProperty(DefaultTraits.Honor));
            Assert.Equal(0, saved.GetPropertyValue(DefaultTraits.Honor));
        });
        Client.Call(() => Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 500));
        Accept();
        Client.Call(() => Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var saved));
            Assert.True(saved.HasProperty(DefaultTraits.Honor));
            Assert.Equal(0, saved.GetPropertyValue(DefaultTraits.Honor));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedAcceptanceCannotStartHonorUpdatesWithoutAServerBaseline(bool hasOtherTrait)
    {
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(instance, playerId).SetTraitLevel(DefaultTraits.Honor, 1);
            });

        var generation = 0;
        Server.Call(() =>
        {
            generation = Server.Resolve<IIssueGenerationRegistry>().Bump(Get<Hero>(Server, giverId));
            if (hasOtherTrait)
            {
                var progress = new PropertyOwner<PropertyObject>();
                progress.SetPropertyValue(DefaultTraits.Calculating, 37);
                GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(Get<Hero>(Server, playerId), progress);
            }
        });
        Client.Call(() =>
        {
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 1500);
            var giver = Get<Hero>(Client, giverId);
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, Controller));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
        });
        Server.Call(() => Assert.Null(Get<Hero>(Server, giverId).Issue.IssueQuest));
        Client.Call(() => TraitLevelingHelper.OnIssueSolvedThroughQuest(Get<Hero>(Client, giverId), DefaultTraits.Honor, 20));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRivalCapturedTraitProgress>());
        Server.Call(() =>
        {
            var found = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var progress);
            Assert.Equal(hasOtherTrait, found);
            if (found)
            {
                Assert.False(progress.HasProperty(DefaultTraits.Honor));
                Assert.Equal(37, progress.GetPropertyValue(DefaultTraits.Calculating));
            }
            Assert.Equal(1, Get<Hero>(Server, playerId).GetTraitLevel(DefaultTraits.Honor));
        });
        Client.Call(() =>
        {
            Assert.Equal(1520, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(1, Get<Hero>(Client, playerId).GetTraitLevel(DefaultTraits.Honor));
            Client.Resolve<IIssueGenerationRegistry>().SetGeneration(Get<Hero>(Client, giverId), generation);
        });
        Accept();
        Client.Call(() => TraitLevelingHelper.OnIssueSolvedThroughQuest(Get<Hero>(Client, giverId), DefaultTraits.Honor, 20));
        Server.Call(() =>
        {
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress
                .TryGet(Get<Hero>(Server, playerId), out var progress));
            Assert.Equal(1540, progress.GetPropertyValue(DefaultTraits.Honor));
        });
        Client.Call(() => Assert.Equal(1540, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
    }

    [Fact]
    public void RejectedAcceptanceRemovesTheStartedQuestAndItsUnownedIssue()
    {
        Client.Call(() => Client.Resolve<ILordWantsRivalCapturedQuestService>().PrepareAcceptance(Get<Hero>(Client, giverId)));
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            var strategy = QuestTypeRegistry.Get(giver.Issue).GetQuestSolutionAcceptMirror<RivalCapturedAcceptFields>();
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(Controller, out var player));
            QuestSolutionStartRunner.RunGuarded(player, () =>
            {
                strategy.ReplayQuestAccepted(giver);
                var quest = GetQuest(Server);
                Assert.True(quest.IsOngoing);
                Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                strategy.RejectAcceptance(giver);
                Assert.True(quest.IsFinalized);
                Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
                return true;
            });
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(Get<Hero>(instance, giverId).Issue));
    }
}
