using Common.Network;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Lord Wants Rival Captured on the issue framework, with the real vanilla preconditions: two players in the giver's
/// kingdom with clan tier 2 and 50 troops, and a target lord outside it.
/// </summary>
public class LordWantsRivalCapturedTests : IDisposable
{
    private const string IssueName = "LordWantsRivalCapturedIssue";
    private const string ControllerA = "rival-player-A";
    private const string ControllerB = "rival-player-B";
    private const int RewardGold = 5000;

    private readonly string kingdomId;
    private readonly string settlementId;
    private readonly string troopId;
    private readonly string targetId;
    private readonly string giverId;
    private readonly Player playerA;
    private readonly Player playerB;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public LordWantsRivalCapturedTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        targetId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        playerA = CreatePlayer(ControllerA);
        playerB = CreatePlayer(ControllerB);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var target = Get<Hero>(instance, targetId);
                var targetParty = Get<MobileParty>(instance, targetPartyId);

                using (new AllowedThread())
                {
                    // The quest's texts read encyclopedia links
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();

                    target.Clan.SetLeader(target);
                    target.PartyBelongedTo = targetParty;
                    targetParty.ActualClan = target.Clan;
                    targetParty.MemberRoster.AddToCounts(target.CharacterObject, 1);

                    foreach (var player in new[] { playerA, playerB })
                    {
                        var hero = Get<Hero>(instance, player.HeroId);
                        var party = Get<MobileParty>(instance, player.MobilePartyId);

                        hero.Clan.SetLeader(hero);
                        hero.Clan._kingdom = Get<Kingdom>(instance, kingdomId);
                        hero.Clan._tier = 2;
                        hero.PartyBelongedTo = party;
                        party.ActualClan = hero.Clan;
                        party.CurrentSettlement = Get<Settlement>(instance, settlementId);
                        party.MemberRoster.AddToCounts(Get<CharacterObject>(instance, troopId), 50);
                    }
                }

                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerA));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerB));
            });
        }

        Connect(ClientA, playerA);
        Connect(ClientB, playerB);

        giverId = AddGiverWithIssue();
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void TheQuestIsRegisteredLikeTheOtherFrameworkQuests()
    {
        var descriptor = Descriptor();

        Assert.Equal(typeof(Issue), descriptor.IssueType);
        Assert.NotNull(descriptor.CreationCaptureStrategy);
        Assert.NotNull(descriptor.QuestSolutionAcceptStrategy);
        Assert.Null(descriptor.AlternativeSolutionAcceptStrategy);
        Assert.NotNull(descriptor.FinalizationProofStrategy);
    }

    [Fact]
    public void CreationCapture_GivesEveryClientTheServersTarget()
    {
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.Equal(typeof(Issue).Name, created.IssueTypeName);

        foreach (var client in new[] { ClientA, ClientB })
        {
            client.Call(() =>
            {
                var issue = Assert.IsType<Issue>(Get<Hero>(client, giverId).Issue);
                Assert.Same(Get<Hero>(client, targetId), issue._targetHero);
                Assert.Equal(created.IssueId, issue.StringId);
            });
        }
    }

    [Fact]
    public void DebugCapture_IsRefusedWhenNoLordIsAtWarWithTheGiver()
    {
        Server.Call(() => Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(Get<Hero>(Server, giverId), out _)));
    }

    [Fact]
    public void AnEligibleAccept_IsNotRefusedAndOnlyTheOwnerRunsTheQuest()
    {
        Accept(ClientA, giverId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(ControllerA, accepted.ControllerId);

        ClientA.Call(() => Assert.Contains(QuestOf(ClientA, giverId), Campaign.Current.QuestManager.Quests));
        ClientB.Call(() =>
        {
            var issue = Get<Hero>(ClientB, giverId).Issue;
            Assert.True(issue.IsSolvingWithQuest);
            Assert.Null(issue.IssueQuest);
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
        });
    }

    [Fact]
    public void EachPlayer_CanHoldARivalQuestAtTheSameTime()
    {
        var secondGiverId = AddGiverWithIssue();

        Accept(ClientA, giverId);
        Accept(ClientB, secondGiverId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());

        foreach (var instance in Instances)
        {
            Assert.Equal(ControllerA, OwnerOf(instance, giverId));
            Assert.Equal(ControllerB, OwnerOf(instance, secondGiverId));
        }

        // Each player's own quest counts as a similar commitment for a third one
        var thirdGiverId = AddGiverWithIssue();
        Assert.False(CheckPreconditions(ClientA, thirdGiverId));
        Assert.False(CheckPreconditions(ClientB, thirdGiverId));
    }

    [Fact]
    public void ThePlayerHoldingARivalQuest_IsRefusedASecondOne()
    {
        var secondGiverId = AddGiverWithIssue();

        Accept(ClientA, giverId);

        // The other player's quest does not count for this player, the player's own does
        Assert.True(CheckPreconditions(ClientB, secondGiverId));
        Assert.False(CheckPreconditions(ClientA, secondGiverId));

        // Skips the dialog's own check so the server decides
        ClientA.Call(() => Get<Hero>(ClientA, secondGiverId).Issue.StartIssueWithQuest());

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(IssueIdOf(secondGiverId), rejected.IssueId);
        Assert.Null(OwnerOf(Server, secondGiverId));
        Assert.Equal(ControllerA, OwnerOf(Server, giverId));
        Server.Call(() => Assert.True(Get<Hero>(Server, secondGiverId).Issue.IsOngoingWithoutQuest));

        // The refused issue is still open to an eligible player
        Accept(ClientB, secondGiverId);
        Assert.Equal(ControllerB, OwnerOf(Server, secondGiverId));
    }

    [Fact]
    public void TheFirstCounterOffer_MakesTheTargetTheOwnersPrisonerOnTheServer()
    {
        Accept(ClientA, giverId);
        AllocateTargetPrisonerLoot();

        ClientA.Call(() =>
        {
            var quest = QuestOf(ClientA, giverId);
            quest.FirstCounterOfferFinished();

            Assert.True(quest._firstCounterOfferMade);
        });

        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            Assert.True(target.IsPrisoner);
            Assert.Same(Get<MobileParty>(Server, playerA.MobilePartyId).Party, target.PartyBelongedToAsPrisoner);
            Assert.True(QuestOf(Server, giverId)._firstCounterOfferMade);
        });
    }

    [Fact]
    public void AValidOwnerCannotCaptureAFreeTargetWithoutAnAllocatedCapture()
    {
        Accept(ClientA, giverId);
        ClientA.SimulateMessage(this, new CampaignTick());
        TestEnvironment.FlushCoalescer();
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        var gold = GoldOnServer(playerA);
        string[] journal = null;
        MobileParty targetParty = null;

        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            targetParty = target.PartyBelongedTo;
            journal = QuestOf(Server, giverId).JournalEntries.Select(log => log.LogText.ToString()).ToArray();
            Assert.Single(journal);
        });

        ClientA.Call(() => QuestOf(ClientA, giverId).FirstCounterOfferFinished());

        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            Assert.False(target.IsPrisoner);
            Assert.Null(target.PartyBelongedToAsPrisoner);
            Assert.Equal(1, targetParty.MemberRoster.GetTroopCount(target.CharacterObject));
            Assert.Equal(gold, Get<Hero>(Server, playerA.HeroId).Gold);
            Assert.Equal(journal, QuestOf(Server, giverId).JournalEntries.Select(log => log.LogText.ToString()).ToArray());
            Assert.True(QuestOf(Server, giverId).IsOngoing);
            Assert.False(QuestOf(Server, giverId)._firstCounterOfferMade);
        });

        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestAcceptanceJournal>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void TheFirstCounterOffer_DoesNotTakeAnotherCaptorsPrisoner()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<Settlement>(Server, settlementId).Party);

        ClientA.Call(() => QuestOf(ClientA, giverId).FirstCounterOfferFinished());

        Server.Call(() =>
        {
            Assert.Same(Get<Settlement>(Server, settlementId).Party, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner);
            Assert.False(QuestOf(Server, giverId)._firstCounterOfferMade);
        });
    }

    [Fact]
    public void TargetEscapingAfterLootAllocation_ClearsPendingCapture()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        AllocateTargetPrisonerLoot();

        Server.Call(() =>
        {
            EndCaptivityAction.ApplyByEscape(Get<Hero>(Server, targetId));
            Assert.False(Get<Hero>(Server, targetId).IsPrisoner);
        });

        ClientA.Call(() => QuestOf(ClientA, giverId).FirstCounterOfferFinished());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() =>
        {
            Assert.True(QuestOf(Server, giverId).IsOngoing);
            Assert.False(QuestOf(Server, giverId)._firstCounterOfferMade);
            Assert.False(Get<Hero>(Server, targetId).IsPrisoner);
        });
    }

    [Fact]
    public void DisposedFinalizationStrategy_DoesNotRecordLootEntitlement()
    {
        Accept(ClientA, giverId);

        LordWantsRivalCapturedFinalizationProofStrategy strategy = null;
        Server.Call(() =>
        {
            strategy = Server.Resolve<LordWantsRivalCapturedFinalizationProofStrategy>();
            strategy.Dispose();
        });

        AllocateTargetPrisonerLoot();

        Server.Call(() =>
        {
            using (new MainHeroSubstitutionScope(Get<Hero>(Server, playerA.HeroId), Get<MobileParty>(Server, playerA.MobilePartyId)))
            {
                Assert.True(strategy.TryRunBranch(QuestOf(Server, giverId), (byte)LordWantsRivalCapturedBranch.CapturedAtCounterOffer));
            }

            Assert.False(Get<Hero>(Server, targetId).IsPrisoner);
            Assert.True(QuestOf(Server, giverId).IsOngoing);
            Assert.False(QuestOf(Server, giverId)._firstCounterOfferMade);
        });
    }

    [Fact]
    public void AcceptingTheCounterOffer_PaysTheOwnerReleasesTheTargetAndBetraysTheGiver()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        var gold = GoldOnServer(playerA);

        ClientA.Call(() => QuestOf(ClientA, giverId).QuestFailCounterOfferAccepted());

        Assert.Equal(gold + (2 * RewardGold), GoldOnServer(playerA));
        Server.Call(() => Assert.False(Get<Hero>(Server, targetId).IsPrisoner));
        AssertFinalizedEverywhere(IssueOutcome.QuestBetrayal);
    }

    [Fact]
    public void AcceptingTheCounterOffer_IsRefusedWhenTheOwnerDoesNotHoldTheTarget()
    {
        Accept(ClientA, giverId);
        var gold = GoldOnServer(playerA);

        ClientA.Call(() => QuestOf(ClientA, giverId).QuestFailCounterOfferAccepted());

        Assert.Equal(gold, GoldOnServer(playerA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.True(QuestOf(Server, giverId).IsOngoing));
    }

    [Fact]
    public void TheOwnersDelivery_IsSentToTheServer()
    {
        Accept(ClientA, giverId);

        ClientA.Call(() =>
        {
            var quest = QuestOf(ClientA, giverId);
            quest.PlayerDeliveredPrisonerQuestSuccess();

            Assert.True(quest.IsOngoing);
        });

        // No conversation with the giver is open, so it is the guard's hand-in
        var request = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal((byte)LordWantsRivalCapturedBranch.DeliveredToGuard, request.Proof);
    }

    [Fact]
    public void GenericSuccessRequest_IsRefusedWithoutTheDeliveryProof()
    {
        Accept(ClientA, giverId);
        var gold = GoldOnServer(playerA);

        ClientA.Call(() => QuestOf(ClientA, giverId).CompleteQuestWithSuccess());
        TestEnvironment.FlushCoalescer();

        Assert.Equal(gold, GoldOnServer(playerA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.True(QuestOf(Server, giverId).IsOngoing));
        ClientA.Call(() => Assert.Contains(QuestOf(ClientA, giverId), Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void DeliveringToTheGiver_PaysTheOwnerAndHandsThePrisonerOver()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        var gold = GoldOnServer(playerA);

        Assert.True(RunBranchAsOwner(playerA, giverId, LordWantsRivalCapturedBranch.DeliveredToGiver));

        Assert.Equal(gold + RewardGold, GoldOnServer(playerA));
        Server.Call(() => Assert.Same(Get<Hero>(Server, giverId).PartyBelongedTo.Party, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner));
        AssertFinalizedEverywhere(IssueOutcome.QuestSuccess);
    }

    [Fact]
    public void DeliveringToTheGiver_IsRefusedWhenTheOwnerIsNotWithTheGiver()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        var gold = GoldOnServer(playerA);

        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Get<MobileParty>(Server, playerA.MobilePartyId).CurrentSettlement = null;
            }
        });

        Assert.True(RunBranchAsOwner(playerA, giverId, LordWantsRivalCapturedBranch.DeliveredToGiver));

        Assert.Equal(gold, GoldOnServer(playerA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Same(Get<MobileParty>(Server, playerA.MobilePartyId).Party, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner));
    }

    [Fact]
    public void DeliveringToTheGuard_IsRefusedOutsideAFortificationOfTheGiversClan()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        var gold = GoldOnServer(playerA);

        Assert.True(RunBranchAsOwner(playerA, giverId, LordWantsRivalCapturedBranch.DeliveredToGuard));

        Assert.Equal(gold, GoldOnServer(playerA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void TheTargetKilledByTheOwner_PaysTheOwnerAndSucceeds()
    {
        Accept(ClientA, giverId);
        var gold = GoldOnServer(playerA);

        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            Get<Hero>(Server, targetId),
            Get<Hero>(Server, playerA.HeroId),
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle,
            showNotification: false));

        Assert.Equal(gold + (RewardGold / 2), GoldOnServer(playerA));
        AssertFinalizedEverywhere(IssueOutcome.QuestSuccess);
    }

    [Fact]
    public void TheTargetKilledBySomeoneElse_CancelsTheQuest()
    {
        Accept(ClientA, giverId);
        var gold = GoldOnServer(playerA);

        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            Get<Hero>(Server, targetId),
            Get<Hero>(Server, playerB.HeroId),
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle,
            showNotification: false));

        Assert.Equal(gold, GoldOnServer(playerA));
        AssertFinalizedEverywhere(IssueOutcome.QuestCancel);
    }

    [Fact]
    public void TheGiverCapturingTheTarget_CancelsTheQuest()
    {
        Accept(ClientA, giverId);

        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(
            Get<Hero>(Server, giverId).PartyBelongedTo.Party,
            Get<Hero>(Server, targetId)));

        AssertFinalizedEverywhere(IssueOutcome.QuestCancel);
    }

    [Fact]
    public void TheOwnerLeavingTheGiversKingdom_CancelsTheQuest()
    {
        Accept(ClientA, giverId);

        Server.Call(() =>
        {
            var clan = Get<Hero>(Server, playerA.HeroId).Clan;

            using (new AllowedThread())
            {
                clan._kingdom = null;
            }

            CampaignEventDispatcher.Instance.OnClanChangedKingdom(
                clan,
                Get<Kingdom>(Server, kingdomId),
                null,
                ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom,
                showNotification: false);
        });

        AssertFinalizedEverywhere(IssueOutcome.QuestCancel);
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void AWarBetweenTheGiverAndTheOwner_EndsTheQuest(DeclareWarAction.DeclareWarDetail detail, IssueOutcome outcome)
    {
        Accept(ClientA, giverId);

        Server.Call(() =>
        {
            var kingdom = Get<Kingdom>(Server, kingdomId);
            var clan = Get<Hero>(Server, playerA.HeroId).Clan;

            using (new AllowedThread())
            {
                clan._kingdom = null;
                FactionManager.DeclareWar(kingdom, clan);
            }

            CampaignEventDispatcher.Instance.OnWarDeclared(kingdom, clan, detail);
        });

        AssertFinalizedEverywhere(outcome);
    }

    [Fact]
    public void DeliveringToTheGiverInAMapConversation_GoesThroughTheServerOnce()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        MeetTheGiverOnTheMap(engage: true);
        var gold = GoldOnServer(playerA);
        var issueId = IssueIdOf(giverId);

        RequestDeliveryToTheGiver(issueId);

        Assert.Equal(gold + RewardGold, GoldOnServer(playerA));
        Server.Call(() => Assert.Same(Get<Hero>(Server, giverId).PartyBelongedTo.Party, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner));
        AssertFinalizedEverywhere(IssueOutcome.QuestSuccess);

        // A repeated request finds no owned issue any more
        RequestDeliveryToTheGiver(issueId);

        Assert.Equal(gold + RewardGold, GoldOnServer(playerA));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void DeliveringToTheGiverOnTheMap_IsRefusedWithoutAConversation()
    {
        Accept(ClientA, giverId);
        TakePrisonerOnServer(Get<MobileParty>(Server, playerA.MobilePartyId).Party);
        MeetTheGiverOnTheMap(engage: false);
        var gold = GoldOnServer(playerA);

        RequestDeliveryToTheGiver(IssueIdOf(giverId));

        Assert.Equal(gold, GoldOnServer(playerA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Same(Get<MobileParty>(Server, playerA.MobilePartyId).Party, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner));
    }

    [Fact]
    public void TheOwnersClient_LeavesEveryWorldEventToTheServer()
    {
        Accept(ClientA, giverId);

        ClientA.Call(() =>
        {
            var quest = QuestOf(ClientA, giverId);
            var target = Get<Hero>(ClientA, targetId);
            var kingdom = Get<Kingdom>(ClientA, kingdomId);
            var clan = Hero.MainHero.Clan;

            // Each of these would end the quest or change the world if it ran here
            quest.OnHeroKilled(target, Hero.MainHero, KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false);
            quest.OnPrisonerTaken(Get<Hero>(ClientA, giverId).PartyBelongedTo.Party, target);
            quest.OnWarDeclared(kingdom, clan, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
            quest.OnClanChangedKingdom(clan, kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            quest.OnMapEventStarted(null, null, null);
            quest.OnPlayerBattleEventEnded(null);

            Assert.True(quest.IsOngoing);
        });

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
    }

    [Theory]
    [InlineData(nameof(CampaignEventDispatcher.OnWarDeclared))]
    [InlineData(nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [InlineData(nameof(CampaignEventDispatcher.OnHeroKilled))]
    [InlineData(nameof(CampaignEventDispatcher.OnMapEventStarted))]
    public void SharedWorldEvents_AreLeftToTheFrameworkRelay(string dispatcherEvent)
    {
        // The framework's QuestEventRelay already runs the quest's own listener, a quest postfix would run it twice
        var patches = Harmony.GetPatchInfo(AccessTools.Method(typeof(CampaignEventDispatcher), dispatcherEvent));
        Assert.NotNull(patches);
        Assert.DoesNotContain(patches.Postfixes, patch => patch.PatchMethod.DeclaringType?.Namespace?.Contains("LordWantsRivalCaptured") == true);
        Assert.Contains(patches.Postfixes, patch => patch.PatchMethod.DeclaringType?.Name == "QuestWorldEventRelayPatches");
    }

    // The giver and the owner both leave the settlement, and the server holds the owner's map conversation with the giver's party when engaged
    private void MeetTheGiverOnTheMap(bool engage)
    {
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            var ownerParty = Get<MobileParty>(Server, playerA.MobilePartyId);

            using (new AllowedThread())
            {
                giver.PartyBelongedTo.CurrentSettlement = null;
                giver.StayingInSettlement = null;
                ownerParty.CurrentSettlement = null;
            }

            Assert.Null(giver.CurrentSettlement);

            if (!engage)
            {
                return;
            }

            Assert.True(Server.ObjectManager.TryGetId(ownerParty.Party, out var ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(giver.PartyBelongedTo.Party, out var giverPartyId));
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                ClientA.NetPeer,
                ownerPartyId,
                giver.PartyBelongedTo,
                giverPartyId,
                engagerIsDefender: false));
        });
    }

    // What the owner's client sends when the giver's delivery dialogue ends
    private void RequestDeliveryToTheGiver(string issueId)
    {
        ClientA.Call(() => ClientA.Resolve<INetwork>().SendAll(
            new RequestQuestBranch(giverId, issueId, (byte)LordWantsRivalCapturedBranch.DeliveredToGiver)));
    }

    private Player CreatePlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        return new Player(controllerId, heroId, partyId, "", "");
    }

    private void Connect(EnvironmentInstance client, Player player)
    {
        TestEnvironment.ConnectRegisteredPlayer(client, player.ControllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(player.ControllerId);

        client.Call(() =>
        {
            var hero = Get<Hero>(client, player.HeroId);

            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = hero.CharacterObject;
                Campaign.Current.MainParty = Get<MobileParty>(client, player.MobilePartyId);
                Campaign.Current.PlayerDefaultFaction = hero.Clan;
            }

            Assert.Same(hero, Hero.MainHero);
        });
    }

    // A giver in the players' kingdom and settlement, given a rival issue by the server
    private string AddGiverWithIssue()
    {
        var newGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var giverPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var giver = Get<Hero>(instance, newGiverId);
                var giverParty = Get<MobileParty>(instance, giverPartyId);
                var settlement = Get<Settlement>(instance, settlementId);

                using (new AllowedThread())
                {
                    giver.Clan.SetLeader(giver);
                    giver.Clan._kingdom = Get<Kingdom>(instance, kingdomId);
                    giver.PartyBelongedTo = giverParty;
                    giverParty.ActualClan = giver.Clan;
                    giverParty.CurrentSettlement = settlement;
                    giver.StayingInSettlement = settlement;
                }
            });
        }

        Server.Call(() =>
        {
            var target = Get<Hero>(Server, targetId);
            var data = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, target), typeof(Issue), IssueBase.IssueFrequency.Rare);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in data, Get<Hero>(Server, newGiverId)));
        });

        return newGiverId;
    }

    // The accept dialog starts the issue's quest, then its consequence starts the quest on the accepting machine
    private void Accept(EnvironmentInstance client, string issueGiverId)
    {
        client.Call(() =>
        {
            var giver = Get<Hero>(client, issueGiverId);
            Assert.True(giver.Issue.StartIssueWithQuest());

            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            quest.QuestAcceptedConsequences();
        });

        Server.Call(() => Assert.NotNull(QuestOf(Server, issueGiverId)));
    }

    private bool CheckPreconditions(EnvironmentInstance client, string issueGiverId)
    {
        var result = false;

        client.Call(() =>
        {
            var giver = Get<Hero>(client, issueGiverId);
            result = giver.Issue.CheckPreconditions(giver, out _);
        });

        return result;
    }

    private bool RunBranchAsOwner(Player owner, string issueGiverId, LordWantsRivalCapturedBranch branch)
    {
        var ran = false;

        Server.Call(() =>
        {
            var hero = Get<Hero>(Server, owner.HeroId);

            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                ran = Descriptor().FinalizationProofStrategy.TryRunBranch(QuestOf(Server, issueGiverId), (byte)branch);
            }
        });

        return ran;
    }

    private void TakePrisonerOnServer(PartyBase captor)
    {
        Server.Call(() =>
        {
            TakePrisonerAction.Apply(captor, Get<Hero>(Server, targetId));
            Assert.Same(captor, Get<Hero>(Server, targetId).PartyBelongedToAsPrisoner);
        });
    }

    private void AllocateTargetPrisonerLoot()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetHandle(Get<Hero>(Server, targetId).CharacterObject, out var targetHandle));
            Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerPrisonersLooted(
                    playerA,
                    new TroopRosterData(new[] { new TroopRosterElementData(targetHandle, 1, 0, 0) })));
        });
    }

    private int GoldOnServer(Player player)
    {
        var gold = 0;
        Server.Call(() => gold = Get<Hero>(Server, player.HeroId).Gold);
        return gold;
    }

    private void AssertFinalizedEverywhere(IssueOutcome outcome)
    {
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() => Assert.Null(Get<Hero>(instance, giverId).Issue));
        }

        Assert.Null(OwnerOf(Server, giverId));
        ClientA.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
    }

    private string IssueIdOf(string issueGiverId)
    {
        var issueId = string.Empty;
        Server.Call(() => issueId = Get<Hero>(Server, issueGiverId).Issue.StringId);
        return issueId;
    }

    private string? OwnerOf(EnvironmentInstance instance, string issueGiverId)
    {
        string? controllerId = null;

        instance.Call(() =>
        {
            var issue = Get<Hero>(instance, issueGiverId).Issue;
            if (issue != null && instance.ObjectManager.TryGetId(issue.IssueOwner, out var issueOwnerId))
            {
                instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(issueOwnerId, issue.StringId, out controllerId);
            }
        });

        return controllerId;
    }

    private static Quest QuestOf(EnvironmentInstance instance, string issueGiverId)
    {
        return Assert.IsType<Quest>(Get<Hero>(instance, issueGiverId).Issue.IssueQuest);
    }

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private static T Get<T>(EnvironmentInstance instance, string id)
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
