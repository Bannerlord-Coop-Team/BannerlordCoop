using Autofac;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.Smugglers;
using GameInterface.Services.Issues.Quests.Smugglers.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace E2E.Tests.Services.Issues;

public class SmugglersTests : IDisposable
{
    private const string IssueName = "SmugglersIssue";
    private const string IssueId = "issue_smugglers";
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const int RewardGold = 1000;
    private const int BribeAmount = 750;

    private readonly MethodCallRecorder succeedQuest;
    private readonly MethodCallRecorder successLogs;
    private readonly MethodCallRecorder hourlyTickParty;
    private readonly MethodCallRecorder cancelQuest;

    private readonly string issueOwnerId;
    private readonly string partyId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();

    public SmugglersTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // Last, so the patches under test decide before the recorder skips the original
        succeedQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.SucceedQuest)));
        hourlyTickParty = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnHourlyTickParty)));
        cancelQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel)));

        // The success logs read the quest giver's character and the town name, which these quests leave unset
        successLogs = new MethodCallRecorder(
            Priority.Last,
            AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessWithPersuasionLog)),
            AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessWithBribeLog)));

        issueOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        RegisterPlayer(ClientA, ControllerA);
        RegisterPlayer(ClientB, ControllerB);

        AddIssue(Server, withQuest: true);
        AddIssue(ClientA, withQuest: true);
        AddIssue(ClientB, withQuest: false);

        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(issueOwnerId, IssueId, ControllerA)));
    }

    public void Dispose()
    {
        succeedQuest.Dispose();
        hourlyTickParty.Dispose();
        cancelQuest.Dispose();
        successLogs.Dispose();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroThatIsNotALord()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var hero));

            Assert.False(Descriptor(Server).CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void CreationCapture_RebuildsTheIssueBetweenTheSameTowns()
    {
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var originId = TestEnvironment.CreateRegisteredObject<Settlement>();
        byte[] captured = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(originId, out var origin));

            using (new AllowedThread())
            {
                captured = Descriptor(Server).CreationCaptureStrategy.Capture(new Issue(issueOwner, new KeyValuePair<Settlement, Settlement>(target, origin)));
            }
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(originId, out var origin));

            Issue issue;
            using (new AllowedThread())
            {
                issue = (Issue)Descriptor(ClientA).CreationCaptureStrategy.CreateIssue(issueOwner, captured);
            }

            Assert.Same(target, issue._targetSettlement);
            Assert.Same(origin, issue._originSettlement);
        });
    }

    [Fact]
    public void Persuasion_IsRunOnTheServerForTheOwner()
    {
        ClientA.Call(() => QuestOf(ClientA).SucceedQuest(null));

        Assert.Equal(1, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Persuasion_FromAPlayerThatDoesNotOwnTheQuest_IsNotRun()
    {
        AttachQuest(ClientB);

        ClientB.Call(() => QuestOf(ClientB).SucceedQuest(null));

        Assert.Equal(0, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Bribe_IsNotRunWhenTheOwnerCannotPay()
    {
        var gold = RunBribeAsOwner(ownerGold: BribeAmount - 1);

        Assert.Equal(BribeAmount - 1, gold);
        Assert.Equal(0, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Bribe_IsPaidByTheOwner()
    {
        var gold = RunBribeAsOwner(ownerGold: BribeAmount + 10);

        Assert.Equal(10, gold);
        Assert.Equal(1, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void PartyCreated_PointsTheOwnersQuestAtTheParty()
    {
        SendPartyCreated(ControllerA);

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, QuestOf(ClientA)._smugglerParty);
        });
        ClientB.Call(() => Assert.Null(IssueOf(ClientB).IssueQuest));
    }

    [Fact]
    public void PartyCreated_LeavesTheQuestOfAPlayerWhoseAcceptLost()
    {
        AttachQuest(ClientB);

        SendPartyCreated(ControllerA);

        ClientB.Call(() => Assert.Null(QuestOf(ClientB)._smugglerParty));
    }

    [Fact]
    public void ClientAccept_UsesTheServersPartyInsteadOfSpawningOne()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var quest = QuestOf(ClientA);

            Assert.Null(quest.CreateSmugglerParty());

            quest._smugglerParty = party;

            Assert.Same(party, quest.CreateSmugglerParty());
        });
    }

    [Fact]
    public void SmugglersRoute_RunsOncePerHourOnTheServersQuest()
    {
        TickParty(Server, usedByQuest: true);
        TickParty(Server, usedByQuest: true);

        Assert.Equal(2, hourlyTickParty.CountFor(Server));
    }

    [Fact]
    public void MapEventEnded_WithMissingSides_DoesNotBlockOtherListeners()
    {
        var receiver = new MapEventEndedReceiver();

        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                QuestOf(Server)._questState = QuestBase.QuestStates.Finalized;
            }

            CampaignEventDispatcher.Instance.AddCampaignEventReceiver(receiver);
            CampaignEventDispatcher.Instance.OnMapEventEnded(ObjectHelper.SkipConstructor<MapEvent>());
        });

        Assert.Equal(1, receiver.CallCount);
    }

    [Fact]
    public void SmugglersRoute_SkipsPartiesNotUsedByAQuest()
    {
        TickParty(Server, usedByQuest: false);

        Assert.Equal(0, hourlyTickParty.CountFor(Server));
    }

    // The framework relays wars and kingdom changes to every registered quest, a relay of Smugglers' own would run them twice
    [Fact]
    public void War_ReachesTheServersQuestOnce()
    {
        using var calls = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));

            CampaignEventDispatcher.Instance.OnWarDeclared(giver.MapFaction, giver.MapFaction, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
        });

        Assert.Equal(1, calls.CountFor(Server));
    }

    [Fact]
    public void KingdomChange_ReachesTheServersQuestOnce()
    {
        using var calls = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var giver));

            CampaignEventDispatcher.Instance.OnClanChangedKingdom(giver.Clan, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        });

        Assert.Equal(1, calls.CountFor(Server));
    }

    [Fact]
    public void SmugglersBeatenWithoutTheOwner_CancelTheQuest()
    {
        EndBattleAgainstTheSmugglers(BattleState.AttackerVictory);

        Assert.Equal(1, cancelQuest.CountFor(Server));
    }

    [Fact]
    public void SmugglersWinningWithoutTheOwner_KeepTheQuest()
    {
        using var failQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.FailQuest)));

        EndBattleAgainstTheSmugglers(BattleState.DefenderVictory);

        Assert.Equal(0, cancelQuest.CountFor(Server));
        Assert.Equal(0, failQuest.CountFor(Server));
    }

    [Fact]
    public void OwnerCapturedByTheirSmugglers_IsReleasedAndFailsTheQuest()
    {
        var releaseMethod = AccessTools.Method(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyByReleasedAfterBattle));
        using var failQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.FailQuest)));
        using var release = new MethodCallRecorder(Priority.Last, releaseMethod);
        var harmony = new Harmony($"smugglers-release-{Guid.NewGuid()}");
        harmony.Patch(releaseMethod, prefix: new HarmonyMethod(typeof(SmugglersTests), nameof(RecordPlayerTroopAtRelease)) { priority = Priority.First });
        playerTroopAtRelease = null;

        try
        {
            var ownerCharacter = CaptureOwnerBy(partyId);
            EndBattleAgainstTheSmugglers(BattleState.DefenderVictory);

            Assert.Equal(1, release.CountFor(Server));
            Assert.Equal(1, failQuest.CountFor(Server));
            Assert.Equal(0, cancelQuest.CountFor(Server));

            // Inside the owner's scope the server would take the owner for its own main hero and grant it captivity XP
            Assert.NotSame(ownerCharacter, playerTroopAtRelease);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static BasicCharacterObject? playerTroopAtRelease;

    private static void RecordPlayerTroopAtRelease() => playerTroopAtRelease = Game.Current?.PlayerTroop;

    [Fact]
    public void OwnerCapturedByAPartyThatJoinedTheSmugglers_FailsTheQuestWithoutReleasingThem()
    {
        var joinedPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        using var failQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.FailQuest)));
        using var release = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyByReleasedAfterBattle)));

        CaptureOwnerBy(joinedPartyId);
        EndBattleAgainstTheSmugglers(BattleState.DefenderVictory, mapEvent =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinedPartyId, out var joinedParty));
            joinedParty.Party.MapEventSide = mapEvent.DefenderSide;
        });

        Assert.Equal(1, failQuest.CountFor(Server));
        Assert.Equal(0, release.CountFor(Server));
    }

    [Fact]
    public void OwnerHeldBeforeTheBattle_KeepsTheQuest()
    {
        using var failQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.FailQuest)));
        using var release = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyByReleasedAfterBattle)));

        CaptureOwnerBy(partyId);
        // The test campaign has no time model, so durations are zero and the battle starts one raw tick after the capture
        EndBattleAgainstTheSmugglers(BattleState.DefenderVictory, mapEvent => mapEvent._mapEventStartTime = new CampaignTime(CampaignTime.Now.NumTicks + 1));

        Assert.Equal(0, failQuest.CountFor(Server));
        Assert.Equal(0, release.CountFor(Server));
        Assert.Equal(0, cancelQuest.CountFor(Server));
    }

    [Fact]
    public void DismissedIssue_RemovesTheRunningQuestsSmugglers()
    {
        using var destroyParty = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));

        FinalizeIssueWithSmugglers(questFinished: false);

        Assert.Equal(1, destroyParty.CountFor(Server));
    }

    [Fact]
    public void FinishedQuestsIssue_LeavesTheSmugglersToTheQuest()
    {
        using var destroyParty = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));

        FinalizeIssueWithSmugglers(questFinished: true);

        Assert.Equal(0, destroyParty.CountFor(Server));
    }

    [Fact]
    public void SmugglersDialog_OpensOnlyForTheQuestsOwnParty()
    {
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var otherParty));
            var quest = QuestOf(ClientA);
            var conversation = Campaign.Current.ConversationManager;

            // No leader troop and nobody talked to, so vanilla's own check passes for any party
            using (new AllowedThread())
            {
                party.MemberRoster.Clear();
                quest._smugglerParty = party;
            }

            var leaderCondition = SmugglersDialogPatches.FindLeaderCondition();
            Assert.NotNull(leaderCondition);

            try
            {
                conversation._conversationParty = otherParty;
                Assert.False((bool)leaderCondition.Invoke(quest, null));

                conversation._conversationParty = party;
                Assert.True((bool)leaderCondition.Invoke(quest, null));

                // With no known conversation party vanilla's own check decides
                conversation._conversationParty = null;
                Assert.True((bool)leaderCondition.Invoke(quest, null));
            }
            finally
            {
                conversation._conversationParty = null;
            }
        });
    }

    [Fact]
    public void ClientTimeout_LeavesTheFailureToTheServer()
    {
        ClientA.Call(() =>
        {
            var quest = QuestOf(ClientA);

            quest.OnTimedOut();

            Assert.Equal(0, quest.RelationshipChangeWithQuestGiver);
        });
    }

    private int RunBribeAsOwner(int ownerGold)
    {
        var gold = 0;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(ControllerA, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));

            using (new AllowedThread())
            {
                ownerHero.ChangeHeroGold(ownerGold - ownerHero.Gold);
            }

            using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
            {
                Assert.True(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(QuestOf(Server), (byte)SmugglersBranch.SuccessByBribe));
            }

            gold = ownerHero.Gold;
        });

        return gold;
    }

    // Another party attacks the smugglers, the owner is not in the battle
    private void EndBattleAgainstTheSmugglers(BattleState battleState, Action<MapEvent>? beforeEnd = null)
    {
        // The map event visual needs a render context, its constructor is skipped and its Initialize turned off
        var disabledMethods = new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) };

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var smugglers));
            var attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var quest = QuestOf(Server);

            using (new AllowedThread())
            {
                smugglers.SetPartyUsedByQuest(true);
                quest._smugglerParty = smugglers;
            }

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(attacker.Party, smugglers.Party, new FieldBattleEventComponent(mapEvent), MapEvent.BattleTypes.FieldBattle);
            mapEvent.MapEventVisual = null;
            mapEvent.BattleState = battleState;
            beforeEnd?.Invoke(mapEvent);

            CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
        }, disabledMethods);
    }

    private CharacterObject CaptureOwnerBy(string captorPartyId)
    {
        CharacterObject ownerCharacter = null!;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(ControllerA, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));

            using (new AllowedThread())
            {
                ownerHero.ChangeState(Hero.CharacterStates.Prisoner);
                ownerHero.PartyBelongedToAsPrisoner = captor.Party;
                ownerHero.CaptivityStartTime = CampaignTime.Now;
            }

            ownerCharacter = ownerHero.CharacterObject;
        });

        return ownerCharacter;
    }

    private void FinalizeIssueWithSmugglers(bool questFinished)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var smugglers));
            var issue = IssueOf(Server);
            var quest = QuestOf(Server);

            using (new AllowedThread())
            {
                smugglers.IsActive = true;
                quest._smugglerParty = smugglers;

                if (questFinished)
                {
                    quest._questState = QuestBase.QuestStates.Finalized;
                }
            }

            issue.IssueFinalized();
        });
    }

    private void SendPartyCreated(string controllerId)
    {
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkSmugglersPartyCreated(issueOwnerId, IssueId, controllerId, partyId)));
    }

    private void TickParty(EnvironmentInstance instance, bool usedByQuest)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.SetPartyUsedByQuest(usedByQuest);
            }

            CampaignEventDispatcher.Instance.HourlyTickParty(party);
        });
    }

    private void RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, playerPartyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
    }

    private void AddIssue(EnvironmentInstance instance, bool withQuest)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));

            using (new AllowedThread())
            {
                var issue = new Issue(issueOwner, new KeyValuePair<Settlement, Settlement>(null!, null!));
                issue.StringId = IssueId;

                Campaign.Current.IssueManager._issues.Add(issueOwner, issue);
                issueOwner.OnIssueCreatedForHero(issue);
            }
        });

        if (withQuest)
        {
            AttachQuest(instance);
        }
    }

    private void AttachQuest(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            var issue = IssueOf(instance);
            var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
            quest.QuestGiver = issue.IssueOwner;
            AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold)).SetValue(quest, RewardGold);

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                issue.IssueQuest = quest;
            }
        });
    }

    private IssueBase IssueOf(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
        return issueOwner.Issue;
    }

    private Quest QuestOf(EnvironmentInstance instance)
    {
        return Assert.IsType<Quest>(IssueOf(instance).IssueQuest);
    }

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private sealed class MapEventEndedReceiver : CampaignEventReceiver
    {
        public int CallCount { get; private set; }

        public override void OnMapEventEnded(MapEvent mapEvent)
        {
            CallCount++;
        }
    }
}
