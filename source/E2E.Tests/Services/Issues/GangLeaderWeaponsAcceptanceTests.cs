using Common.Util;
using E2E.Tests.Environment;
using Common.Network;
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
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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

    [Fact]
    public void OwnershipSaveRestoresTheSameGuardsOnlyToTheSavedQuest()
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
}
