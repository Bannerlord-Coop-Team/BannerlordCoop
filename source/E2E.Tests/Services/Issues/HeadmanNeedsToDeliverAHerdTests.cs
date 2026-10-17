using Common.Util;
using E2E.Tests.Environment;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace E2E.Tests.Services.Issues;

public class HeadmanNeedsToDeliverAHerdTests : IDisposable
{
    // The harness has no map navigation mesh; real issue construction still runs against the bound town.
    private readonly MethodCallRecorder randomSettlement = new(AccessTools.Method(typeof(SettlementHelper), nameof(SettlementHelper.FindRandomSettlement)));
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string villageId;
    private readonly string townId;
    private readonly string targetId;
    private readonly string livestockId;
    private readonly Player[] players;
    private readonly Dictionary<EnvironmentInstance, Quest> accepted = new();

    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance ClientA => scenario.ClientA;
    private EnvironmentInstance ClientB => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public HeadmanNeedsToDeliverAHerdTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        villageId = environment.CreateRegisteredObject<Settlement>();
        townId = environment.CreateRegisteredObject<Settlement>();
        targetId = environment.CreateRegisteredObject<Hero>();
        livestockId = environment.CreateRegisteredObject<ItemObject>();
        scenario = new QuestScenario(environment, CreateIssue);
        players = Server.Resolve<IPlayerManager>().Players.ToArray();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    foreach (var player in players)
                    {
                        var hero = Object<Hero>(instance, player.HeroId);
                        var party = Object<MobileParty>(instance, player.MobilePartyId);
                        hero._partyBelongedTo = party;
                        hero.Clan.Culture = hero.CharacterObject.Culture;
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        environment.Dispose();
        randomSettlement.Dispose();
    }

    [Fact]
    public void Accept_GrantsTheHerdAndGrainOnceToItsOwner_AndStartsOnlyTheirJournal()
    {
        Accept(ClientA);
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var serverQuest = accepted[Server];
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var owner = Party(instance, QuestScenario.ControllerA);
                Assert.Equal(serverQuest._animalCountToDeliver, owner.ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
                Assert.Equal(5, owner.ItemRoster.GetItemNumber(DefaultItems.Grain));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerB).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
                if (instance != ClientA) Assert.Empty(Campaign.Current.QuestManager.Quests);
            });
        }
        ClientA.Call(() => Assert.Contains(accepted[ClientA], Campaign.Current.QuestManager.Quests));
        Assert.Empty(ClientB.NetworkSentMessages.GetMessages<RequestQuestSolutionAccept>());
    }

    [Theory]
    [InlineData((byte)HeadmanHerdBranch.Deliver, IssueOutcome.QuestSuccess)]
    [InlineData((byte)HeadmanHerdBranch.Keep, IssueOutcome.QuestFail)]
    public void DeliveryDialogue_EndsOnlyTheOwnersQuestOnEveryMachine(byte proof, IssueOutcome expected)
    {
        var branch = (HeadmanHerdBranch)proof;
        Accept(ClientA);
        MoveToDestination();
        ClientA.Call(() =>
        {
            using (OwnerScope(ClientA, QuestScenario.ControllerA))
            {
                if (branch == HeadmanHerdBranch.Deliver) accepted[ClientA].DeliverHerdOnConsequence();
                else accepted[ClientA].DeliverHerdRejectOnConsequence();
            }
        });

        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Empty(Campaign.Current.QuestManager.Quests);
                Assert.Equal(branch == HeadmanHerdBranch.Deliver ? 0 : accepted[Server]._animalCountToDeliver,
                    Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
            });
        }
        ClientA.Call(() => Assert.NotEmpty(accepted[ClientA].JournalEntries));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Delivery_IsRefusedOutsideTheDestinationOrWithoutTheFullHerd(bool insufficient)
    {
        Accept(ClientA);
        if (insufficient)
        {
            MoveToDestination();
            Server.Call(() => Party(Server, QuestScenario.ControllerA).ItemRoster.AddToCounts(Object<ItemObject>(Server, livestockId), -1));
        }

        ClientA.Call(() => accepted[ClientA].DeliverHerdOnConsequence());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.True(accepted[ClientA].IsOngoing);
    }

    [Theory]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestTimeOut)]
    public void CancelAndTimeout_RemoveTheRemainingHerdOnce(IssueOutcome outcome)
    {
        Accept(ClientA);
        Server.Call(() => Party(Server, QuestScenario.ControllerA).ItemRoster.AddToCounts(Object<ItemObject>(Server, livestockId), -2));
        ClientA.Call(() =>
        {
            if (outcome == IssueOutcome.QuestCancel) accepted[ClientA].CompleteQuestWithCancel();
            else accepted[ClientA].CompleteQuestWithTimeOut();
        });

        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(0, Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId))));
    }

    [Fact]
    public void TargetDeath_OnTheServerCancelsTheOwnersQuestAndCleansUpTheirHerd()
    {
        Accept(ClientA);
        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(Object<Hero>(Server, targetId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(0, Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId))));
    }

    [Fact]
    public void RealPreconditions_RejectAnInactiveTarget()
    {
        Server.Call(() =>
        {
            var issue = Object<Hero>(Server, scenario.NotableId).Issue;
            var player = players.Single(player => player.ControllerId == QuestScenario.ControllerA);
            var owner = Object<Hero>(Server, player.HeroId);
            Assert.True(Server.Resolve<IIssueAcceptPreconditions>().CanTake(issue, owner, player.ControllerId));
            using (new AllowedThread()) Object<Hero>(Server, targetId).ChangeState(Hero.CharacterStates.Dead);
            Assert.False(Server.Resolve<IIssueAcceptPreconditions>().CanTake(issue, owner, player.ControllerId));
        });
    }

    [Fact]
    public void TwoPlayers_CanHoldSeparateHerds_AndFinishingOneLeavesTheOtherRunning()
    {
        Accept(ClientA);
        var secondGiver = scenario.AddIssue();
        ClientB.Call(() =>
        {
            using (OwnerScope(ClientB, QuestScenario.ControllerB))
            {
                var issue = Object<Hero>(ClientB, secondGiver).Issue;
                issue.StartIssueWithQuest();
                issue.IssueQuest.StartQuest();
            }
        });
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());

        ClientA.Call(() => accepted[ClientA].CompleteQuestWithCancel());
        foreach (var instance in Instances)
            instance.Call(() => Assert.True(Object<Hero>(instance, secondGiver).Issue.IsSolvingWithQuest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionSolution_RunsOnServerAndReturnsOnlyTheOwnersTroops(bool fail)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Object<CharacterObject>(instance, troopId).Level = 16;
                    Object<Hero>(instance, companionId)._partyBelongedTo = Party(instance, QuestScenario.ControllerA);
                }
            });
        }
        Server.Call(() =>
        {
            var party = Party(Server, QuestScenario.ControllerA);
            party.MemberRoster.AddToCounts(Object<Hero>(Server, companionId).CharacterObject, 1);
            party.MemberRoster.AddToCounts(Object<CharacterObject>(Server, troopId), 40);
        });
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            using (OwnerScope(ClientA, QuestScenario.ControllerA))
            {
                var issue = Object<Hero>(ClientA, scenario.NotableId).Issue;
                var companion = Object<Hero>(ClientA, companionId);
                var troop = Object<CharacterObject>(ClientA, troopId);
                using (new AllowedThread())
                {
                    Party(ClientA, QuestScenario.ControllerA).MemberRoster.AddToCounts(companion.CharacterObject, -1);
                    Party(ClientA, QuestScenario.ControllerA).MemberRoster.AddToCounts(troop, -40);
                    issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    issue.AlternativeSolutionSentTroops.AddToCounts(troop, 40);
                }
                issue.StartIssueWithAlternativeSolution();
            }
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        Server.Call(() =>
        {
            var issue = Object<Hero>(Server, scenario.NotableId).Issue;
            // Force the inherited mutually exclusive endings without depending on the random draw.
            issue._failureChance = fail ? 1f : -1f;
            issue.CompleteIssueWithAlternativeSolution();
        });
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.AlternativeSolution, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(40, Party(instance, QuestScenario.ControllerA).MemberRoster.GetTroopCount(Object<CharacterObject>(instance, troopId)));
                Assert.True(Party(instance, QuestScenario.ControllerA).MemberRoster.Contains(Object<Hero>(instance, companionId).CharacterObject));
                Assert.Empty(Campaign.Current.QuestManager.Quests);
                Assert.Equal(0, Party(instance, QuestScenario.ControllerB).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
            });
        }
    }

    private IssueBase CreateIssue(EnvironmentInstance instance, Hero giver)
    {
        if (Campaign.Current.EncyclopediaManager == null)
        {
            Campaign.Current.EncyclopediaManager = new EncyclopediaManager();
        }
        Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
        var villageSettlement = Object<Settlement>(instance, villageId);
        var townSettlement = Object<Settlement>(instance, townId);
        var target = Object<Hero>(instance, targetId);
        var item = Object<ItemObject>(instance, livestockId);
        if (villageSettlement.Village == null)
        {
            var town = new Town();
            townSettlement.SetSettlementComponent(town);
            townSettlement.Town = town;
            var village = new Village();
            village.Bound = townSettlement;
            villageSettlement.SetSettlementComponent(village);
            villageSettlement.Village = village;
            town.Security = 50;
            target.StayingInSettlement = townSettlement;
            townSettlement.AddHeroWithoutParty(target);
            target.Occupation = Occupation.Merchant;
            item.Name = new TextObject("sheep");
            item.Value = 100;
        }
        giver.StayingInSettlement = villageSettlement;
        giver.Occupation = Occupation.Headman;
        townSettlement.Town._ownerClan = giver.Clan;
        var issue = new Issue(giver);
        issue._targetSettlement = townSettlement;
        issue._targetHero = target;
        issue._herdTypeToDeliver = item;
        return issue;
    }

    private void Accept(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            using (OwnerScope(client, QuestScenario.ControllerA))
            {
                Object<Hero>(client, scenario.NotableId).Issue.StartIssueWithQuest();
                var quest = (Quest)Object<Hero>(client, scenario.NotableId).Issue.IssueQuest;
                quest.QuestAcceptedConsequences();
            }
        });
        Server.Call(() => accepted[Server] = (Quest)Object<Hero>(Server, scenario.NotableId).Issue.IssueQuest);
        client.Call(() => accepted[client] = (Quest)Object<Hero>(client, scenario.NotableId).Issue.IssueQuest);
    }

    private void MoveToDestination()
    {
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread()) Party(instance, QuestScenario.ControllerA).CurrentSettlement = Object<Settlement>(instance, townId);
            });
    }

    private MobileParty Party(EnvironmentInstance instance, string controller)
        => Object<MobileParty>(instance, players.Single(player => player.ControllerId == controller).MobilePartyId);

    private MainHeroSubstitutionScope OwnerScope(EnvironmentInstance instance, string controller)
        => new(Object<Hero>(instance, players.Single(player => player.ControllerId == controller).HeroId), Party(instance, controller));

    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
