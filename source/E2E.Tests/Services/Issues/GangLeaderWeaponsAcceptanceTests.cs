using Common.Util;
using E2E.Tests.Environment;
using Common.Tests.Utils;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

public sealed class GangLeaderWeaponsAcceptanceTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public GangLeaderWeaponsAcceptanceTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    private string CreateIssueCopies(bool includeServer = false)
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var instances = includeServer ? environment.Clients.Append(environment.Server) : environment.Clients;
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.GangLeader;
                    Campaign.Current.MainParty = party;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var runner = new CreationCaptureRunner<Issue, GangLeaderWeaponsCreationFields>(
                        instance.Resolve<IGangLeaderWeaponsAcceptance>(), IssueBase.IssueFrequency.Common);
                    runner.ConstructAndRegisterReplicated(giver,
                        new GangLeaderWeaponsCreationFields(0, 200, CampaignTime.Days(15)));
                }
            });
        }
        return giverId;
    }

    [Fact]
    public void MirrorPreservesAuthoritativeProgressWithEmptyObserverInventory()
    {
        var giverId = CreateIssueCopies();
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.Empty(MobileParty.MainParty.ItemRoster);
                var fields = new GangLeaderWeaponsQuestFields("weapons_authoritative_quest",
                    CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17);

                client.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver, fields);

                var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                Assert.Equal(fields.QuestId, quest.StringId);
                Assert.Equal(fields.DueTime, quest.QuestDueTime);
                Assert.Equal(17, quest._collectedItemAmount);
                Assert.Equal(17, quest._playerStartsQuestLog.CurrentProgress);
                Assert.Equal(21, quest._requestedWeaponAmount);
                Assert.Equal(1050, quest._bribeGold);
                Assert.Equal(4700, quest.RewardGold);
                Assert.Equal(0.75f, quest._issueDifficulty);
                Assert.Empty(MobileParty.MainParty.ItemRoster);
            });
        }
    }

    [Fact]
    public void CancelReturnsConfiscatedWeaponsOnceWithoutChangingAnotherPlayersInventory()
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = environment.CreateRegisteredObject<MobileParty>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var otherParty));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("weapons-cancel-owner", ownerId, partyId, "", "")));
            server.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver,
                new GangLeaderWeaponsQuestFields("weapons_cancel_quest", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            server.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "weapons-cancel-owner");
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            using (new AllowedThread())
            {
                otherParty.ItemRoster.AddToCounts(item, 11);
                quest._weaponsThatGuardTook.Add(new EquipmentElement(item), 7);
            }

            quest.CompleteQuestWithCancel();

            Assert.False(quest.IsOngoing);
            Assert.Equal(7, party.ItemRoster.GetItemNumber(item));
            Assert.Equal(11, otherParty.ItemRoster.GetItemNumber(item));
            quest.CompleteQuestWithCancel();
            Assert.Equal(7, party.ItemRoster.GetItemNumber(item));
            Assert.Equal(11, otherParty.ItemRoster.GetItemNumber(item));
        });
    }

    [Fact]
    public void DuplicateEligibilityIgnoresAnotherPlayersQuestButKeepsOwnCommitmentGate()
    {
        var activeGiverId = CreateIssueCopies(includeServer: true);
        var offeredGiverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(activeGiverId, out var activeGiver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(offeredGiverId, out var offeredGiver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("weapons-player", ownerId, partyId, "", "")));
            server.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(activeGiver,
                new GangLeaderWeaponsQuestFields("another_players_weapons", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            var ownership = server.Resolve<IIssueOwnershipRegistry>();
            ownership.SetOwner(activeGiver, "another-player");
            using (new MainHeroSubstitutionScope(owner, party))
            {
                Assert.True(offeredGiver.Issue.IssueQuestCanBeDuplicated);
                ownership.SetOwner(activeGiver, "weapons-player");
                Assert.False(offeredGiver.Issue.IssueQuestCanBeDuplicated);
                ownership.Clear(activeGiver);
                Assert.False(offeredGiver.Issue.IssueQuestCanBeDuplicated);
            }
            Assert.True(activeGiver.Issue.IssueQuest.IsOngoing);
            Assert.True(offeredGiver.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void GuardBribeDebitsOnlyTheResolvedOwnerAndRejectsInsufficientGold()
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var otherOwnerId = environment.CreateRegisteredObject<Hero>();
        var ownerPartyId = environment.CreateRegisteredObject<MobileParty>();
        var guardsId = environment.CreateRegisteredObject<MobileParty>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(otherOwnerId, out var otherOwner));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(guardsId, out var guards));
            server.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver,
                new GangLeaderWeaponsQuestFields("weapons_bribe_quest", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            using (new AllowedThread())
            {
                party.CurrentSettlement = giver.CurrentSettlement;
                guards.IsActive = true;
                guards.IsVisible = false;
                owner.Gold = quest._bribeGold - 1;
                otherOwner.Gold = 9876;
                quest._guardsParty = guards;
            }
            var actions = server.Resolve<IGangLeaderWeaponsQuestActions>();
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.Bribe));
            Assert.Equal(quest._bribeGold - 1, owner.Gold);
            Assert.False(quest._playerDodgedGuards);
            using (new AllowedThread()) owner.Gold = quest._bribeGold + 123;

            Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.Bribe));

            Assert.Equal(123, owner.Gold);
            Assert.Equal(9876, otherOwner.Gold);
            Assert.True(quest._playerDodgedGuards);
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.Bribe));
            Assert.Equal(123, owner.Gold);
        });
    }

    [Fact]
    public void CompanionAcceptanceReceiverAppliesFrozenDifficultyBeforeChangingIssueState()
    {
        var giverId = CreateIssueCopies();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var state = new AlternativeSolutionVanillaState(CampaignTime.Days(20), CampaignTime.Days(30), 0f, 0, 120f, null);
        var fields = new GangLeaderWeaponsAlternativeFields(0.75f, state);
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            var roster = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread()) roster.AddToCounts(companion.CharacterObject, 1);
            var troops = environment.Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(roster);
            environment.Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeAlternativeAccepted(
                giverId, "weapons-owner", state, GenericAcceptFieldsSerializer.Serialize(fields), troops));
        });

        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(0.75f, giver.Issue._issueDifficultyMultiplier);
                Assert.Equal(state.ReturnTime, giver.Issue.AlternativeSolutionReturnTimeForTroops);
                Assert.Equal(state.EffectClearTime, giver.Issue.AlternativeSolutionIssueEffectClearTime);
                Assert.Equal(120f, giver.Issue._totalTroopXpAmount);
                Assert.Same(companion, giver.Issue.AlternativeSolutionHero);
                Assert.True(client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out var owner));
                Assert.Equal("weapons-owner", owner);
            });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompanionOutcomeReplicatesTheActualJournalAndReturnsOnlyTheOwnersTroops(int outcome)
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var otherGiverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        const string controller = "weapons-companion-owner";
        var server = environment.Server;
        var ownerClient = environment.Clients.Last();
        ownerClient.Resolve<IControllerIdProvider>().SetControllerId(controller);
        var state = new AlternativeSolutionVanillaState(CampaignTime.Zero, CampaignTime.Zero, 0f, 0, 120f, "trade");

        foreach (var instance in environment.Clients.Append(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                using (new AllowedThread())
                {
                    giver.CurrentSettlement.Town.OwnerClan = clan;
                    giver.CurrentSettlement.Town.Security = 80f;
                    owner.Gold = 100;
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    instance.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, controller);
                    instance.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, 4);
                    instance.Resolve<IGangLeaderWeaponsAcceptance>().MirrorAlternativeAccepted(giver,
                        new GangLeaderWeaponsAlternativeFields(0.75f, state));
                    giver.Issue._companionRewardSkill = DefaultSkills.Trade;
                    instance.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(otherGiver,
                        new GangLeaderWeaponsQuestFields("unrelated_personal_weapons", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
                }
            });
        }
        server.Call(() => Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player(controller, ownerId, partyId, "", ""))));
        environment.ConnectRegisteredPlayer(ownerClient, controller);

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            var issue = Assert.IsType<Issue>(giver.Issue);
            var reward = issue.RewardGold;
            var random = Game.Current.RandomGenerator;
            var before = (random._x, random._y, random._z, random._w);
            try
            {
                // The installed zero-failure-chance comparison still fails on an exact zero roll.
                random._x = outcome == 1 ? 0u : 1u;
                random._y = 2;
                random._z = 3;
                random._w = 0;
                if (outcome == 2) issue.CompleteIssueWithCancel();
                else AlternativeSolutionCompletionRunner.CompleteOnServer(giver, issue);
            }
            finally
            {
                (random._x, random._y, random._z, random._w) = before;
            }

            Assert.Null(giver.Issue);
            Assert.Equal(100 + (outcome == 0 ? reward : 0), owner.Gold);
            Assert.Equal(outcome == 0 ? 50f : 80f, giver.CurrentSettlement.Town.Security);
            Assert.True(server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controller, out var returned));
            Assert.Equal(1, returned.TotalManCount);
            Assert.False(server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("another-player", out _));
            AlternativeSolutionCompletionRunner.CompleteOnServer(giver, issue);
            Assert.Equal(100 + (outcome == 0 ? reward : 0), owner.Gold);
            Assert.Equal(1, returned.TotalManCount);
        });

        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
                Assert.Null(giver.Issue);
                var otherQuest = Assert.IsType<Quest>(otherGiver.Issue.IssueQuest);
                Assert.True(otherQuest.IsOngoing);
                Assert.Equal(17, otherQuest._playerStartsQuestLog.CurrentProgress);
                var ended = Campaign.Current.LogEntryHistory.GetGameActionLogs((JournalLogEntry log) => log.RelatedHero == giver && log.IsEnded()).ToArray();
                if (client == ownerClient)
                {
                    var journal = Assert.Single(ended);
                    Assert.Equal(outcome != 0, journal.IsEndedUnsuccessfully());
                    Assert.True(journal.GetEntries().Count() >= 2);
                    Assert.True(client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controller, out var returned));
                    Assert.Equal(1, returned.TotalManCount);
                }
                else Assert.Empty(ended);
            });
        }
        var removal = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(outcome == 0 ? IssueFinalizeReason.AlternativeSolutionSuccess :
            outcome == 1 ? IssueFinalizeReason.AlternativeSolutionFail : IssueFinalizeReason.IssueOnly, removal.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnershipSaveRestoresTheSameGuardsOnlyToTheSavedQuest(bool detached)
    {
        var giverId = CreateIssueCopies();
        var guardsId = environment.CreateRegisteredObject<MobileParty>();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(guardsId, out var guards));
            var acceptance = client.Resolve<IGangLeaderWeaponsAcceptance>();
            acceptance.MirrorQuestAccepted(giver, new GangLeaderWeaponsQuestFields("weapons_saved_quest",
                CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            quest._guardsParty = guards;
            quest._checkForBattleResult = true;
            if (detached) Campaign.Current.IssueManager._issues.Remove(giver);
            var saved = new IssueOwnershipSaveData(giver, "quest-owner");
            quest._guardsParty = null;
            quest._checkForBattleResult = false;

            saved.RestoreQuestReferences();

            Assert.Same(guards, quest._guardsParty);
            Assert.True(quest._checkForBattleResult);
            quest._guardsParty = null;
            quest._checkForBattleResult = false;
            saved.WeaponsQuestId = "a-different-quest";
            saved.RestoreQuestReferences();
            Assert.Null(quest._guardsParty);
            Assert.False(quest._checkForBattleResult);
        });
    }

    [Fact]
    public void DuplicateAcceptanceDoesNotReplaceQuestOrResetProgress()
    {
        var giverId = CreateIssueCopies();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var acceptance = client.Resolve<IGangLeaderWeaponsAcceptance>();
            var fields = new GangLeaderWeaponsQuestFields("weapons_duplicate_quest",
                CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17);
            acceptance.MirrorQuestAccepted(giver, fields);
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            quest.SetCurrentItemAmount(20);
            var journal = quest._playerStartsQuestLog;

            acceptance.MirrorQuestAccepted(giver, fields);

            Assert.Same(quest, giver.Issue.IssueQuest);
            Assert.Same(journal, quest._playerStartsQuestLog);
            Assert.Equal(20, quest._collectedItemAmount);
            Assert.Equal(20, journal.CurrentProgress);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MirroredAcceptanceLeavesOnlyTheQuestOwnersConversation(bool isOwner)
    {
        var giverId = CreateIssueCopies();
        var clanId = environment.CreateRegisteredObject<Clan>();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            using (new AllowedThread())
            {
                Hero.MainHero.Clan = clan;
                giver.CurrentSettlement.Town.OwnerClan = clan;
            }
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._encounteredParty = giver.CurrentSettlement.Party;
            Campaign.Current.PlayerEncounter = encounter;
            Helpers.MapEventHelper.OnConversationEnd();
            Assert.True(PlayerEncounter.LeaveEncounter);
            PlayerEncounter.LeaveEncounter = false;
            client.Resolve<IControllerIdProvider>().SetControllerId("weapons-conversation-owner");
            var localController = client.Resolve<IControllerIdProvider>().ControllerId;
            client.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, isOwner ? localController : "another-controller");
            try
            {
                client.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver,
                    new GangLeaderWeaponsQuestFields("weapons_conversation", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));

                Assert.Equal(isOwner, PlayerEncounter.LeaveEncounter);
                Assert.Same(encounter, PlayerEncounter.Current);
                var journal = new QuestsVM(() => { });
                if (isOwner)
                {
                    Assert.Same(giver.Issue.IssueQuest, Assert.Single(journal.ActiveQuestsList).Quest);
                    Assert.NotNull(journal.SelectedQuest);
                }
                else
                {
                    Assert.Empty(journal.ActiveQuestsList);
                    Assert.Null(journal.SelectedQuest);
                    Assert.False(journal.IsThereAnyQuest);
                }
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });
    }

    [Fact]
    public void CompanionAcceptRejectsAnIneligibleOwnerBeforeTakingGoldOrTroops()
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var issue = Assert.IsType<Issue>(giver.Issue);
            var needed = issue.GetTotalAlternativeSolutionNeededMenCount();
            var selection = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                owner.Clan = clan;
                giver.CurrentSettlement.Town.OwnerClan = clan;
                owner.Gold = 100000;
                troop.Level = 20;
                party.MemberRoster.AddToCounts(troop, needed);
                selection.AddToCounts(troop, needed);
            }
            var player = new Player("weapons-ineligible-owner", ownerId, partyId, "", "");
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(player));
            using (new MainHeroSubstitutionScope(owner, party))
            using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
                Assert.True(issue.AlternativeSolutionCondition(out _));

            var troopsBefore = party.MemberRoster.TotalManCount;
            Assert.Throws<InvalidOperationException>(() => AlternativeSolutionStartRunner.StartOnServerFromClaim(giver, player, selection));

            Assert.Equal(100000, owner.Gold);
            Assert.Equal(troopsBefore, party.MemberRoster.TotalManCount);
            Assert.Empty(issue.AlternativeSolutionSentTroops.GetTroopRoster());
            Assert.True(issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void LoadedOrphanCancellationFinalizesBothMirrorsAndReturnsWeaponsOnlyOnce()
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        const string questId = "weapons_orphaned_quest";
        var ownerClient = environment.Clients.Last();
        ownerClient.Resolve<IControllerIdProvider>().SetControllerId("weapons-orphan-owner");
        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player("weapons-orphan-owner", ownerId, partyId, "", "")));
                using (new AllowedThread())
                {
                    item.StringId = itemId;
                    MBObjectManager.Instance.RegisterObject(item);
                }
                (Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>() ?? new JournalLogsCampaignBehavior()).RegisterEvents();
                instance.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "weapons-orphan-owner");
                Assert.Equal(instance == ownerClient, instance.Resolve<IIssueOwnershipRegistry>().IsLocalPeerOwner(giver));
                instance.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, 4);
                instance.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver,
                    new GangLeaderWeaponsQuestFields(questId, CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
                var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                if (instance == ownerClient)
                    Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs((JournalLogEntry log) => log.RelatedHero == giver));
                quest._weaponsThatGuardTook.Add(new EquipmentElement(item), 7);
                Campaign.Current.IssueManager._issues.Remove(giver);
            });
        }

        environment.Server.Call(() => Campaign.Current.QuestManager.OnGameLoaded(null));
        environment.Server.Call(() => environment.Server.Resolve<ISendCoalescer>().Flush(environment.Server.Resolve<INetwork>()));

        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, q => q.StringId == questId);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                Assert.Equal(7, party.ItemRoster.GetItemNumber(item));
                var history = Campaign.Current.LogEntryHistory.GetGameActionLogs((JournalLogEntry log) => log.RelatedHero == giver && log.IsEnded());
                if (instance == ownerClient) Assert.Single(history);
                else Assert.Empty(history);
            });
        }
        var removal = Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(questId, removal.QuestId);
        Assert.Equal(4, removal.Generation);
        Assert.Equal(IssueFinalizeReason.QuestCancel, removal.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayerCharacterChangeCancelsOnlyThatPlayersQuestAndReturnsWeaponsToTheNewParty(bool networkChange)
    {
        var giverId = CreateIssueCopies(includeServer: true);
        var otherGiverId = CreateIssueCopies(includeServer: true);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var otherOwnerId = environment.CreateRegisteredObject<Hero>();
        var successorId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = environment.CreateRegisteredObject<MobileParty>();
        var newPartyId = environment.CreateRegisteredObject<MobileParty>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(successorId, out var successor));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var oldParty));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(newPartyId, out var newParty));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            var players = server.Resolve<IPlayerManager>();
            Assert.True(players.AddPlayer(new Player("changing-owner", networkChange ? successorId : ownerId,
                networkChange ? newPartyId : partyId, "", "")));
            Assert.True(players.AddPlayer(new Player("unaffected-owner", otherOwnerId, otherPartyId, "", "")));
            var acceptance = server.Resolve<IGangLeaderWeaponsAcceptance>();
            acceptance.MirrorQuestAccepted(giver, new GangLeaderWeaponsQuestFields("weapons_changed_owner", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            acceptance.MirrorQuestAccepted(otherGiver, new GangLeaderWeaponsQuestFields("weapons_unchanged_owner", CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17));
            server.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, "changing-owner");
            server.Resolve<IIssueOwnershipRegistry>().SetOwner(otherGiver, "unaffected-owner");
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            var otherQuest = Assert.IsType<Quest>(otherGiver.Issue.IssueQuest);
            quest._weaponsThatGuardTook.Add(new EquipmentElement(item), 7);

            if (networkChange)
            {
                var client = environment.Clients.Last();
                environment.ConnectRegisteredPlayer(client, "changing-owner");
                client.Call(() => client.Resolve<INetwork>().SendAll(
                    new NetworkPlayerCharacterChangedAfterHeirSelection(ownerId, successorId, newPartyId, true)));
            }
            else Campaign.Current.QuestManager.OnPlayerCharacterChanged(owner, successor, newParty, true);

            Assert.False(quest.IsOngoing);
            Assert.True(otherQuest.IsOngoing);
            Assert.Equal(17, otherQuest._playerStartsQuestLog.CurrentProgress);
            Assert.Equal(7, newParty.ItemRoster.GetItemNumber(item));
            Assert.Equal(0, oldParty.ItemRoster.GetItemNumber(item));
        });
    }
}
