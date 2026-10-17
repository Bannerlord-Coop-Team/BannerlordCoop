using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace E2E.Tests.Services.Issues;

public class CapturedByBountyHuntersTests : IDisposable
{
    private const string IssueId = "issue_captured_by_bounty_hunters";
    private const int RewardGold = 3000;
    private const float InitialPower = 100;
    private const float PowerChange = 10;

    private static readonly FieldInfo SideMapEventField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent));

    private static readonly MethodBase[] OutcomeLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.SuccessQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerLostTheFightLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.HideoutClearedBySomeoneElseLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.TimeOutLogText)),
    };

    private readonly string gangLeaderId;
    private readonly string hideoutId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string otherHeroId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public CapturedByBountyHuntersTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        gangLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, "player-A");
        (otherHeroId, _) = RegisterPlayer(ClientB, "player-B");

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));

                using (new AllowedThread())
                {
                    gangLeader.Occupation = Occupation.GangLeader;
                    gangLeader._power = InitialPower;
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void AnIssueTheServerCreatesIsBuiltOnEveryClientWithTheSameHideout()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new Issue(hero, hideout),
                typeof(Issue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, gangLeader));
        });

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.Equal(typeof(Issue).Name, created.IssueTypeName);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
                var issue = Assert.IsType<Issue>(gangLeader.Issue);

                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(hideout, issue._hideout);
            });
        }
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroThatIsNotAGangLeader()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var hero));

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void AcceptCapture_ForcesTheAccepterDueTimeOntoTheServerQuest()
    {
        Server.Call(() =>
        {
            var strategy = Descriptor().QuestSolutionAcceptStrategy;
            var accepterIssue = IssueWithQuest(CampaignTime.Days(40));
            var mirrorIssue = IssueWithQuest(CampaignTime.Days(39));

            strategy.Apply(mirrorIssue, strategy.Capture(accepterIssue));

            Assert.Equal(CampaignTime.Days(40), mirrorIssue.IssueQuest.QuestDueTime);
        });
    }

    [Fact]
    public void AHideoutBattleTheOwnerWinsPaysTheOwnerOnTheServer()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);
        var otherGold = GoldOnServer(otherHeroId);

        EndBattleOnTheServer(BattleState.AttackerVictory, ownerTakesPart: true);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);

        Assert.Equal(ownerGold + RewardGold, GoldOnServer(ownerHeroId));
        Assert.Equal(otherGold, GoldOnServer(otherHeroId));
        AssertGiverPowerChangedOnceBy(PowerChange);
        AssertTheIssueIsGoneEverywhere();
    }

    [Fact]
    public void AHideoutBattleTheOwnerLosesFailsTheQuestOnTheServer()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);

        EndBattleOnTheServer(BattleState.DefenderVictory, ownerTakesPart: true);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);

        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(-PowerChange);
        AssertTheIssueIsGoneEverywhere();
    }

    [Fact]
    public void AHideoutClearedBySomeoneElseFailsTheQuestWithoutConsequences()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);

        EndBattleOnTheServer(BattleState.AttackerVictory, ownerTakesPart: false);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);

        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(0);
        AssertTheIssueIsGoneEverywhere();
    }

    [Fact]
    public void ATimeoutRunsItsConsequencesOnceOnTheServer()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();

        ClientA.Call(() => QuestOnClientA().CompleteQuestWithTimeOut(), OutcomeLogTexts);

        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);

        AssertGiverPowerChangedOnceBy(-PowerChange);
        AssertTheIssueIsGoneEverywhere();
    }

    [Fact]
    public void TheOwnersClientNeverRunsTheBattleConsequences()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);

        ClientA.Call(() =>
        {
            var quest = QuestOnClientA();

            using (new AllowedThread())
            {
                quest.OnMapEventEnded(HideoutBattle(ClientA, BattleState.AttackerVictory));
                quest.SuccessConsequences();
                quest.FailConsequences(isTimedOut: false);
            }

            Assert.True(quest.IsOngoing);
        }, OutcomeLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(0);
    }

    // Only the server runs the consequences, the clients get the change through the power sync
    private void AssertGiverPowerChangedOnceBy(float change)
    {
        foreach (var instance in Instances)
        {
            var expected = instance == Server ? InitialPower + change : InitialPower;

            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.Equal(expected, gangLeader.Power);
            });
        }
    }

    private void AssertTheIssueIsGoneEverywhere()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.Null(gangLeader.Issue);
            });
        }

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(gangLeaderId, IssueId, out _));
        });
    }

    private void EndBattleOnTheServer(BattleState result, bool ownerTakesPart)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var mapEvent = HideoutBattle(Server, result);

            if (ownerTakesPart)
            {
                var attackerSide = (MapEventSide)RuntimeHelpers.GetUninitializedObject(typeof(MapEventSide));
                SideMapEventField.SetValue(attackerSide, mapEvent);
                attackerSide.MissionSide = BattleSideEnum.Attacker;
                ownerParty.Party._mapEventSide = attackerSide;
            }

            try
            {
                CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
            }
            finally
            {
                ownerParty.Party._mapEventSide = null;
            }
        }, OutcomeLogTexts);
    }

    // The hideout has no parties left, as after the bounty hunters were beaten
    private MapEvent HideoutBattle(EnvironmentInstance instance, BattleState result)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));

        var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
        mapEvent.MapEventSettlement = hideout;
        mapEvent._battleState = result;

        return mapEvent;
    }

    private void AcceptOnClientA()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            gangLeader.Issue.StartIssueWithQuest();

            // The offer dialog's consequence starts the owner's quest
            using (new AllowedThread())
            {
                gangLeader.Issue.IssueQuest.StartQuest();
            }

            Assert.Contains(gangLeader.Issue.IssueQuest, Campaign.Current.QuestManager.Quests);
        });

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(gangLeaderId, IssueId, out var owner));
            Assert.Equal("player-A", owner);
        });
    }

    private Quest QuestOnClientA()
    {
        Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
        return Assert.IsType<Quest>(gangLeader.Issue.IssueQuest);
    }

    private int GoldOnServer(string heroId)
    {
        var gold = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            gold = hero.Gold;
        });

        return gold;
    }

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(typeof(Issue).Name, out var descriptor));
        return descriptor;
    }

    private static Issue IssueWithQuest(CampaignTime dueTime)
    {
        var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
        quest.QuestDueTime = dueTime;

        var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
        issue.IssueQuest = quest;

        return issue;
    }

    private (string HeroId, string PartyId) RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            // The server runs the outcome as this hero, with this party as the main party
            using (new AllowedThread())
            {
                hero._partyBelongedTo = party;
            }

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        return (heroId, partyId);
    }

    private void GiveTheGangLeaderTheIssue()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));

                using (new AllowedThread())
                {
                    var issue = new Issue(gangLeader, hideout);
                    issue.StringId = IssueId;
                    issue._issueState = IssueBase.IssueState.Ongoing;

                    Campaign.Current.IssueManager._issues.Add(gangLeader, issue);
                    gangLeader.OnIssueCreatedForHero(issue);
                }
            });
        }
    }
}
