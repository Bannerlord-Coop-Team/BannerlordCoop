using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
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
using E2E.Tests.Util;

namespace E2E.Tests.Services.Issues;

public class CapturedByBountyHuntersTests : IDisposable
{
    private const string IssueId = "issue_captured_by_bounty_hunters";
    private const int RewardGold = 3000;
    private const float InitialPower = 100;
    private const float PowerChange = 10;
    private const int BattleDay = 10;

    private static readonly CampaignTime BattleStartTime = CampaignTime.Days(BattleDay);

    private static readonly FieldInfo SideMapEventField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent));

    // Test heroes and settlements have no encyclopedia links, a missing settlement still throws before these
    private static readonly MethodBase[] AcceptLogLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
        AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLinkWithName)),
    };

    private static readonly MethodBase[] OutcomeLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.SuccessQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerLostTheFightLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.HideoutClearedBySomeoneElseLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.TimeOutLogText)),
    };

    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));
    private readonly string gangLeaderId;
    private readonly string hideoutId;
    private readonly string townId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string otherHeroId;
    private readonly string otherPartyId;

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
        townId = TestEnvironment.CreateRegisteredObject<Settlement>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, "player-A");
        (otherHeroId, otherPartyId) = RegisterPlayer(ClientB, "player-B");

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townId, out var town));

                using (new AllowedThread())
                {
                    gangLeader.Occupation = Occupation.GangLeader;
                    gangLeader._power = InitialPower;
                    gangLeader.StayingInSettlement = town;
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AnIssueTheServerCreatesIsBuiltOnEveryClientWithTheSameHideout()
    {
        var created = CreateIssueOnServer();
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
    public void AnIssueTheServerCreatesCanBeAcceptedByAClientOutsideASettlement()
    {
        // The server also names issues it never sends, so its next id is ahead of the clients' own
        Server.Call(() => Campaign.Current.IssueManager._nextIssueUniqueIndex = 7);

        var created = CreateIssueOnServer();
        Assert.Equal("issue_7", created.IssueId);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
                Assert.Equal(created.IssueId, gangLeader.Issue.StringId);
            });
        }

        AcceptOnClientA(created.IssueId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
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
    public void AHideoutBattleTheOwnerWinsAlongsideAnotherPlayerPaysOnlyTheOwner()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);
        var otherGold = GoldOnServer(otherHeroId);

        EndBattleOnTheServer(BattleState.AttackerVictory, new[] { ownerPartyId, otherPartyId }, otherHeroId, otherPartyId);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);

        Assert.Equal(ownerGold + RewardGold, GoldOnServer(ownerHeroId));
        Assert.Equal(otherGold, GoldOnServer(otherHeroId));
        AssertGiverPowerChangedOnceBy(PowerChange);
    }

    [Fact]
    public void AnOwnerWithoutAPartyFailsWithoutConsequencesWhenAnotherPlayerClears()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);
        RemoveTheOwnersParty();

        EndBattleOnTheServer(BattleState.AttackerVictory, new[] { otherPartyId }, otherHeroId, otherPartyId);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);

        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(0);
        AssertTheIssueIsGoneEverywhere();
    }

    // Co-op removes a defeated player's party before the battle ends, the owner is then the winners' prisoner
    [Fact]
    public void AnOwnerCapturedLosingTheHideoutBattleFailsTheQuestOnTheServer()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        var ownerGold = GoldOnServer(ownerHeroId);
        PutBanditsInTheHideout();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        EndBattleOnTheServer(BattleState.DefenderVictory, Array.Empty<string>(), capture: new OwnerCapture(captorPartyId));

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);

        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(-PowerChange);
        AssertTheIssueIsGoneEverywhere();
    }

    // The owner is another player's prisoner and that player clears the hideout, which is someone else clearing it
    [Fact]
    public void AnOwnerHeldByThePlayerWhoClearsTheHideoutFailsWithoutConsequences()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();

        EndBattleOnTheServer(
            BattleState.AttackerVictory,
            Array.Empty<string>(),
            capture: new OwnerCapture(otherPartyId, BattleSideEnum.Attacker));

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        AssertGiverPowerChangedOnceBy(0);
    }

    [Fact]
    public void AnOwnerCapturedBeforeTheBattleKeepsTheQuestWhenAnotherAssaultFails()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        PutBanditsInTheHideout();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        EndBattleOnTheServer(
            BattleState.DefenderVictory,
            new[] { otherPartyId },
            capture: new OwnerCapture(captorPartyId, BeforeBattle: true));

        AssertTheQuestIsStillRunning();
    }

    [Fact]
    public void AnOwnerHeldByTheDefeatedSideKeepsTheQuestWhileBanditsHoldTheHideout()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        PutBanditsInTheHideout();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        EndBattleOnTheServer(BattleState.AttackerVictory, Array.Empty<string>(), capture: new OwnerCapture(captorPartyId));

        AssertTheQuestIsStillRunning();
    }

    // Held by a party winning another battle elsewhere while another player's assault on the hideout fails
    [Fact]
    public void AnOwnerHeldByAPartyInAnotherBattleKeepsTheQuestWhenTheAssaultFails()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        PutBanditsInTheHideout();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        EndBattleOnTheServer(
            BattleState.DefenderVictory,
            new[] { otherPartyId },
            capture: new OwnerCapture(captorPartyId, CaptorInThisBattle: false));

        AssertTheQuestIsStillRunning();
    }

    [Fact]
    public void AnOwnerCapturedInABattleElsewhereKeepsTheQuest()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        PutBanditsInTheHideout();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        EndBattleOnTheServer(
            BattleState.DefenderVictory,
            Array.Empty<string>(),
            capture: new OwnerCapture(captorPartyId),
            battleSettlementId: townId);

        AssertTheQuestIsStillRunning();
    }

    private void AssertTheQuestIsStillRunning()
    {
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertGiverPowerChangedOnceBy(0);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            Assert.True(gangLeader.Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void TheServerRefusesAnAcceptTheOwnerCouldNotTake()
    {
        GiveTheGangLeaderTheIssue();
        preconditions.Refuse(Server);

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            gangLeader.Issue.StartIssueWithQuest();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(gangLeaderId, IssueId, out _));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            Assert.True(gangLeader.Issue.IsOngoingWithoutQuest);
        });
    }

    [Fact]
    public void AnOwnerWithoutAPartyKeepsTheQuestWhileBanditsHoldTheHideout()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        RemoveTheOwnersParty();
        PutBanditsInTheHideout();

        EndBattleOnTheServer(BattleState.AttackerVictory, new[] { otherPartyId }, otherHeroId, otherPartyId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertGiverPowerChangedOnceBy(0);
    }

    private void PutBanditsInTheHideout()
    {
        var banditPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var bandits));

            using (new AllowedThread())
            {
                hideout._partiesCache.Add(bandits);
            }
        });
    }

    private void RemoveTheOwnersParty()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));

            using (new AllowedThread())
            {
                owner._partyBelongedTo = null;
            }
        });
    }

    [Fact]
    public void ABattleElsewhereWhileBanditsHoldTheHideoutChangesNothing()
    {
        GiveTheGangLeaderTheIssue();
        AcceptOnClientA();
        PutBanditsInTheHideout();

        EndBattleOnTheServer(BattleState.AttackerVictory, ownerTakesPart: false);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertGiverPowerChangedOnceBy(0);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            Assert.True(gangLeader.Issue.IsSolvingWithQuest);
        });
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
            Assert.Single(quest.JournalEntries);
        }, OutcomeLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(ownerGold, GoldOnServer(ownerHeroId));
        AssertGiverPowerChangedOnceBy(0);
    }

    // Only the server runs the consequences, then the resulting power syncs to every client
    private void AssertGiverPowerChangedOnceBy(float change)
    {
        foreach (var instance in Instances)
        {
            var expected = InitialPower + change;

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
        EndBattleOnTheServer(result, ownerTakesPart ? new[] { ownerPartyId } : Array.Empty<string>());
    }

    // The attackers stay attached while the battle ends, as in vanilla's FinalizeEventAux
    private void EndBattleOnTheServer(
        BattleState result,
        string[] attackerPartyIds,
        string? finalizingHeroId = null,
        string? finalizingPartyId = null,
        OwnerCapture? capture = null,
        string? battleSettlementId = null)
    {
        Server.Call(() =>
        {
            var mapEvent = HideoutBattle(Server, result, battleSettlementId);
            var attackerSide = SideOf(mapEvent, BattleSideEnum.Attacker);

            var attackers = attackerPartyIds.Select(partyId =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                return party;
            }).ToArray();

            foreach (var party in attackers)
            {
                party.Party._mapEventSide = attackerSide;
            }

            // A party in the battle that holds the owner prisoner
            MobileParty? captor = null;
            if (capture != null)
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(capture.CaptorPartyId, out captor));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                var captorBattle = capture.CaptorInThisBattle ? mapEvent : HideoutBattle(Server, result);
                captor.Party._mapEventSide = SideOf(captorBattle, capture.CaptorSide);

                using (new AllowedThread())
                {
                    owner._partyBelongedTo = null;
                    owner._heroState = Hero.CharacterStates.Prisoner;
                    owner.PartyBelongedToAsPrisoner = captor.Party;
                    owner.CaptivityStartTime = capture.BeforeBattle ? CampaignTime.Days(BattleDay - 1) : BattleStartTime;
                }
            }

            try
            {
                if (finalizingHeroId == null)
                {
                    CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
                    return;
                }

                // The server finalizes a hideout battle as one of the players in it
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(finalizingHeroId, out var finalizingHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(finalizingPartyId, out var finalizingParty));

                using (new MainHeroSubstitutionScope(finalizingHero, finalizingParty))
                {
                    CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
                    Assert.Same(finalizingParty, Campaign.Current.MainParty);
                }
            }
            finally
            {
                foreach (var party in attackers)
                {
                    party.Party._mapEventSide = null;
                }

                if (captor != null)
                {
                    captor.Party._mapEventSide = null;
                }
            }
        }, OutcomeLogTexts);
    }

    private sealed record OwnerCapture(
        string CaptorPartyId,
        BattleSideEnum CaptorSide = BattleSideEnum.Defender,
        bool BeforeBattle = false,
        bool CaptorInThisBattle = true);

    private static MapEventSide SideOf(MapEvent mapEvent, BattleSideEnum side)
    {
        var mapEventSide = (MapEventSide)RuntimeHelpers.GetUninitializedObject(typeof(MapEventSide));
        SideMapEventField.SetValue(mapEventSide, mapEvent);
        mapEventSide.MissionSide = side;
        return mapEventSide;
    }

    // The hideout has no parties in it, which on the server is the case from the battle after the clear
    private MapEvent HideoutBattle(EnvironmentInstance instance, BattleState result, string? settlementId = null)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId ?? hideoutId, out var settlement));

        var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
        mapEvent.SetMapEventSettlement(settlement);
        mapEvent._battleState = result;
        mapEvent._mapEventStartTime = BattleStartTime;

        return mapEvent;
    }

    private NetworkIssueCreated CreateIssueOnServer()
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

        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
    }

    // Client A is not in a settlement, as when the offer conversation is opened on the map
    private void AcceptOnClientA(string issueId = IssueId)
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(gangLeaderId, out var gangLeader));
            Assert.Null(Settlement.CurrentSettlement);

            gangLeader.Issue.StartIssueWithQuest();

            // The offer dialog's consequence starts the owner's quest and writes its start log
            var quest = Assert.IsType<Quest>(gangLeader.Issue.IssueQuest);
            quest.QuestAcceptedConsequences();

            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
            Assert.Single(quest.JournalEntries);
        }, AcceptLogLinks);

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(gangLeaderId, issueId, out var owner));
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
