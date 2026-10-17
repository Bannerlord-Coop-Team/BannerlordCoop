using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ArmyOfPoachers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArmyOfPoachersTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string villageId;
    private readonly string townId;
    private readonly string poachersId;
    private readonly string townComponentId;
    private readonly string villageComponentId;
    private readonly string menId;
    private readonly Player playerA;
    private readonly Player playerB;
    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, Owner, Other };

    public ArmyOfPoachersTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        villageId = environment.CreateRegisteredObject<Settlement>();
        townId = environment.CreateRegisteredObject<Settlement>();
        poachersId = environment.CreateRegisteredObject<MobileParty>();
        townComponentId = environment.CreateRegisteredObject<Town>();
        villageComponentId = environment.CreateRegisteredObject<Village>();
        menId = environment.CreateRegisteredObject<CharacterObject>();
        scenario = new QuestScenario(environment, CreateIssue);
        Player a = null!;
        Player b = null!;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out a));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerB, out b));
        });
        playerA = a;
        playerB = b;

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    foreach (var player in new[] { playerA, playerB })
                    {
                        var hero = Get<Hero>(instance, player.HeroId);
                        var party = Get<MobileParty>(instance, player.MobilePartyId);
                        hero.PartyBelongedTo = party;
                        hero.Clan.SetLeader(hero);
                        hero.HitPoints = hero.MaxHitPoints;
                        hero.ChangeHeroGold(1000 - hero.Gold);
                        party.CurrentSettlement = Get<Settlement>(instance, villageId);
                        party.IsActive = true;
                        party.MemberRoster.AddToCounts(Get<CharacterObject>(instance, menId), 20);
                    }

                    var giver = Get<Hero>(instance, scenario.NotableId);
                    giver.Clan = Get<Hero>(instance, playerA.HeroId).Clan;
                    giver.CurrentSettlement.Town.OwnerClan = giver.Clan;
                    var poachers = Get<MobileParty>(instance, poachersId);
                    poachers.CurrentSettlement = Get<Settlement>(instance, villageId);
                    poachers.IsActive = true;
                }
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void AcceptStartsOnlyTheOwnersRealQuest()
    {
        Accept();

        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Owner.Call(() => Assert.Contains(QuestOf(Owner), Campaign.Current.QuestManager.Quests));
        Server.Call(() => Assert.DoesNotContain(QuestOf(Server), Campaign.Current.QuestManager.Quests));
        Other.Call(() => Assert.Null(IssueOf(Other).IssueQuest));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Fact]
    public void CreationCaptureKeepsTheServersVillage()
    {
        byte[] captured = null!;
        Server.Call(() => captured = Descriptor(Server).CreationCaptureStrategy.Capture(IssueOf(Server)));
        Owner.Call(() =>
        {
            using (new AllowedThread())
            {
                var rebuilt = (Issue)Descriptor(Owner).CreationCaptureStrategy.CreateIssue(Get<Hero>(Owner, scenario.NotableId), captured);
                Assert.Same(Get<Settlement>(Owner, villageId).Village, rebuilt._questVillage);
            }
        });
    }

    [Fact]
    public void RaidedVillageRefusesAcceptanceOnTheServer()
    {
        Server.Call(() => Get<Settlement>(Server, villageId).Village.VillageState = Village.VillageStates.Looted);

        scenario.Accept(Owner);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Server.Call(() => Assert.True(IssueOf(Server).IsOngoingWithoutQuest));
    }

    [Theory]
    [InlineData((byte)ArmyOfPoachersBranch.Agreement, true, -5f, 50f)]
    [InlineData((byte)ArmyOfPoachersBranch.LeavePoachersAlone, false, -5f, -30f)]
    public void DialogueRunsConsequencesOnceForTheOwner(byte branch, bool rewarded, float securityChange, float prosperityChange)
    {
        Accept();
        AttachParty();
        var quest = QuestOf(Owner);
        Owner.Call(() =>
        {
            if ((ArmyOfPoachersBranch)branch == ArmyOfPoachersBranch.Agreement)
                quest.QuestSuccessPlayerComesToAnAgreementWithPoachers();
            else
                quest.QuestFailedAfterTalkingWithPoachers();
        });

        AssertOutcome(rewarded, securityChange, prosperityChange);
        Owner.Call(() => Owner.Resolve<INetwork>().SendAll(new RequestQuestBranch(scenario.NotableId, QuestScenario.IssueId, branch)));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Owner.Call(() => Assert.NotEmpty(quest.JournalEntries));
    }

    [Theory]
    [InlineData((byte)ArmyOfPoachersBranch.Agreement)]
    [InlineData((byte)ArmyOfPoachersBranch.LeavePoachersAlone)]
    public void AnotherPlayersDialogueCannotFinishTheQuest(byte branch)
    {
        Accept();
        AttachParty();
        Other.Call(() => Other.Resolve<INetwork>().SendAll(new RequestQuestBranch(scenario.NotableId, QuestScenario.IssueId, branch)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.True(QuestOf(Server).IsOngoing);
    }

    [Fact]
    public void LateAgreementAfterLeavingTheVillageDoesNotPay()
    {
        Accept();
        AttachParty();
        Server.Call(() => Get<MobileParty>(Server, playerA.MobilePartyId).CurrentSettlement = null);
        Owner.Call(() => QuestOf(Owner).QuestSuccessPlayerComesToAnAgreementWithPoachers());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void TimeoutRunsPenaltyOnTheServerAndReplaysWithoutRepeatingIt()
    {
        Accept();
        Owner.Call(() => QuestOf(Owner).CompleteQuestWithTimeOut());

        AssertOutcome(false, -5f, -30f);
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartyAnnouncementIsBoundToTheIssueAndAcceptingPlayer(bool stale)
    {
        Accept();
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkPoachersPartyCreated(scenario.NotableId,
            stale ? "issue_old" : QuestScenario.IssueId, QuestScenario.ControllerA, poachersId)));

        Owner.Call(() => Assert.Equal(stale, QuestOf(Owner)._poachersParty == null));
        Other.Call(() => Assert.Null(IssueOf(Other).IssueQuest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VillageBattleAllowsTheOwnerOnEitherSide(bool reversed)
    {
        Accept();
        AttachParty();
        Server.Call(() =>
        {
            var player = Get<MobileParty>(Server, playerA.MobilePartyId);
            var poachers = Get<MobileParty>(Server, poachersId);
            Assert.True(reversed ? MapEventBattleFactory.CanCreateMapEvent(player.Party, poachers.Party, default) :
                MapEventBattleFactory.CanCreateMapEvent(poachers.Party, player.Party, default));
            Assert.False(MapEventBattleFactory.CanCreateMapEvent(Get<MobileParty>(Server, playerB.MobilePartyId).Party, poachers.Party, default));
        });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void BattleResultsUseTheActualOwnerAndParty(bool ownerInBattle, bool poachersLose)
    {
        Accept();
        AttachParty();
        Server.Call(() =>
        {
            var player = Get<MobileParty>(Server, ownerInBattle ? playerA.MobilePartyId : playerB.MobilePartyId);
            var poachers = Get<MobileParty>(Server, poachersId);
            // Both parties are in the village for the quest battle, another player's ordinary battle is outside.
            if (!ownerInBattle) player.CurrentSettlement = poachers.CurrentSettlement = null;
            var battle = MapEventBattleFactory.CreateMapEvent(player.Party, poachers.Party, default);
            Assert.NotNull(battle);
            battle.BattleState = poachersLose ? BattleState.AttackerVictory : BattleState.DefenderVictory;
            battle.FinalizeEvent();
        }, new[] { AccessTools.Method(typeof(MapEvent), nameof(MapEvent.MovePartyToSuitablePositionOnMapEventFinalize)) });

        if (ownerInBattle)
            AssertOutcome(poachersLose, poachersLose ? 0 : -5, poachersLose ? 50 : -30);
        else if (poachersLose)
            Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        else
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void TwoPlayersCanAcceptSeparateCopiesButOnePlayerCannotTakeBoth()
    {
        Accept();
        var giverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, giverId);
                    giver.StayingInSettlement = Get<Settlement>(instance, townId);
                    giver.Clan = Get<Hero>(instance, playerA.HeroId).Clan;
                    var issue = new Issue(giver, Get<Settlement>(instance, villageId).Village);
                    issue.StringId = "issue_second_poachers";
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }

        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, giverId).Issue;
            Assert.False(Server.Resolve<IIssueAcceptPreconditions>().CanTake(issue, Get<Hero>(Server, playerA.HeroId), QuestScenario.ControllerA));
            Assert.True(Server.Resolve<IIssueAcceptPreconditions>().CanTake(issue, Get<Hero>(Server, playerB.HeroId), QuestScenario.ControllerB));
        });
        Other.Call(() =>
        {
            Game.Current.PlayerTroop = Get<Hero>(Other, playerB.HeroId).CharacterObject;
            Get<Hero>(Other, giverId).Issue.StartIssueWithQuest();
        });
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(giverId, "issue_second_poachers", out var owner) && owner == QuestScenario.ControllerB));
    }

    [Fact]
    public void CaptivityProtectionAppliesOnlyToTheOwnerInTheQuestBattle()
    {
        Accept();
        Server.Call(() =>
        {
            QuestOf(Server)._isReadyToBeFinalized = true;
            var ownerCanBeCaptured = true;
            var otherCanBeCaptured = true;
            CampaignEventDispatcher.Instance.CanHeroBecomePrisoner(Get<Hero>(Server, playerA.HeroId), ref ownerCanBeCaptured);
            CampaignEventDispatcher.Instance.CanHeroBecomePrisoner(Get<Hero>(Server, playerB.HeroId), ref otherCanBeCaptured);
            Assert.False(ownerCanBeCaptured);
            Assert.True(otherCanBeCaptured);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompanionSolutionUsesTheFrameworkAndAppliesTheOwnersOutcome(bool success)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var companion = Get<Hero>(instance, companionId);
                    companion.Clan = Get<Hero>(instance, playerA.HeroId).Clan;
                    companion.SetSkillValue(DefaultSkills.Bow, 150);
                    var troop = Get<CharacterObject>(instance, troopId);
                    troop.Level = 15;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                }
            });
        }

        Owner.Call(() =>
        {
            Game.Current.PlayerTroop = Get<Hero>(Owner, playerA.HeroId).CharacterObject;
            var sent = IssueOf(Owner).AlternativeSolutionSentTroops;
            using (new AllowedThread())
            {
                sent.AddToCounts(Get<Hero>(Owner, companionId).CharacterObject, 1);
                sent.AddToCounts(Get<CharacterObject>(Owner, troopId), 40);
            }
            IssueOf(Owner).StartIssueWithAlternativeSolution();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Server.Call(() =>
        {
            var issue = IssueOf(Server);
            Assert.True(issue.IsSolvingWithAlternative);
            issue._failureChance = success ? -1 : 1;
            issue._alternativeSolutionCasualtyCount = 0;
            issue.CompleteIssueWithAlternativeSolution();
        });

        AssertOutcome(success, success ? 0 : -5, success ? 50 : -30);
        Assert.Equal(IssueOutcome.AlternativeSolution, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private IssueBase CreateIssue(EnvironmentInstance instance, Hero giver)
    {
        var town = Get<Town>(instance, townComponentId);
        var village = Get<Village>(instance, villageComponentId);
        var townSettlement = Get<Settlement>(instance, townId);
        var villageSettlement = Get<Settlement>(instance, villageId);
        townSettlement.SetSettlementComponent(town);
        villageSettlement.SetSettlementComponent(village);
        village.Bound = townSettlement;
        town.Security = 50;
        town.Prosperity = 1000;
        giver.StayingInSettlement = townSettlement;
        giver.Occupation = Occupation.Merchant;
        Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
        Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
        var journals = new JournalLogsCampaignBehavior();
        Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new[] { journals }));
        journals.RegisterEvents();
        return new Issue(giver, village);
    }

    private void Accept()
    {
        Owner.Call(() => Game.Current.PlayerTroop = Get<Hero>(Owner, playerA.HeroId).CharacterObject);
        scenario.Accept(Owner);
        Owner.Call(() => QuestOf(Owner).QuestAcceptedConsequences());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    private void AttachParty()
    {
        Server.Call(() => QuestOf(Server)._poachersParty = Get<MobileParty>(Server, poachersId));
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkPoachersPartyCreated(scenario.NotableId,
            QuestScenario.IssueId, QuestScenario.ControllerA, poachersId)));
    }

    private void AssertOutcome(bool rewarded, float securityChange, float prosperityChange)
    {
        Server.Call(() =>
        {
            Assert.Equal(rewarded ? 1800 : 1000, Get<Hero>(Server, playerA.HeroId).Gold);
            Assert.Equal(1000, Get<Hero>(Server, playerB.HeroId).Gold);
            var town = Get<Settlement>(Server, townId).Town;
            Assert.Equal(50 + securityChange, town.Security);
            Assert.Equal(1000 + prosperityChange, town.Prosperity);
        });
        Owner.Call(() => Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, quest => quest is Quest));
        Other.Call(() => Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, quest => quest is Quest));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    private IssueBase IssueOf(EnvironmentInstance instance) => Get<Hero>(instance, scenario.NotableId).Issue;
    private Quest QuestOf(EnvironmentInstance instance) => (Quest)scenario.QuestOf(instance);

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
        return descriptor;
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
