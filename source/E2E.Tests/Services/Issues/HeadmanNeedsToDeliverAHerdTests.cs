using Common.Util;
using E2E.Tests.Environment;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Registry.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
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
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace E2E.Tests.Services.Issues;

public class HeadmanNeedsToDeliverAHerdTests : IDisposable
{
    // The harness has no map navigation mesh; real issue construction still runs against the bound town.
    private readonly MethodCallRecorder randomSettlement = new(AccessTools.Method(typeof(SettlementHelper), nameof(SettlementHelper.FindRandomSettlement)));
    internal readonly E2ETestEnvironment environment;
    internal readonly QuestScenario scenario;
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
        Server.Call(() =>
        {
            foreach (var player in players) Party(Server, player.ControllerId).ItemRoster.Clear();
        });
        environment.FlushCoalescer();
    }

    public void Dispose()
    {
        try
        {
            environment.Dispose();
        }
        finally
        {
            randomSettlement.Dispose();
        }
    }

    [Fact]
    public void Accept_GrantsTheHerdAndGrainOnceToItsOwner_AndStartsOnlyTheirJournal()
    {
        using var notices = new MethodCallRecorder(AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        Accept(ClientA);
        Assert.Equal(1, notices.CountFor(ClientA));
        Assert.Equal(0, notices.CountFor(ClientB));
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

    [Fact]
    public void IssueCreation_ReplicasUseTheServersIdentityDestinationRecipientAndAnimal()
    {
        Server.Call(() =>
        {
            var giver = Object<Hero>(Server, scenario.NotableId);
            giver.Issue.CompleteIssueWithCancel();
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => CreateIssue(Server, owner),
                typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var issue = Assert.IsType<Issue>(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(Object<Settlement>(instance, townId), issue._targetSettlement);
                Assert.Same(Object<Hero>(instance, targetId), issue._targetHero);
                Assert.Same(Object<ItemObject>(instance, livestockId), issue._herdTypeToDeliver);
            });
        }
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void WarDeclaredOnServer_UsesTheOwnersFactionToChooseFailureOrCancel(DeclareWarAction.DeclareWarDetail detail, IssueOutcome expected)
    {
        Accept(ClientA);
        Server.Call(() => DeclareWarAction.ApplyInternal(Object<Hero>(Server, scenario.NotableId).Clan,
            PlayerHero(Server, QuestScenario.ControllerA).Clan, detail));
        environment.FlushCoalescer();
        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue));
    }

    [Theory]
    [InlineData((byte)HeadmanHerdBranch.Deliver, IssueOutcome.QuestSuccess)]
    [InlineData((byte)HeadmanHerdBranch.Keep, IssueOutcome.QuestFail)]
    public void DeliveryDialogue_EndsOnlyTheOwnersQuestOnEveryMachine(byte proof, IssueOutcome expected)
    {
        var branch = (HeadmanHerdBranch)proof;
        Accept(ClientA);
        MoveToDestination();
        var gold = new Dictionary<EnvironmentInstance, (int Owner, int Other)>();
        foreach (var instance in Instances)
            instance.Call(() => gold[instance] = (PlayerHero(instance, QuestScenario.ControllerA).Gold, PlayerHero(instance, QuestScenario.ControllerB).Gold));
        ClientA.Call(() =>
        {
            using (OwnerScope(ClientA, QuestScenario.ControllerA))
            {
                if (branch == HeadmanHerdBranch.Deliver) accepted[ClientA].DeliverHerdOnConsequence();
                else accepted[ClientA].DeliverHerdRejectOnConsequence();
            }
        });
        environment.FlushCoalescer();

        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Empty(Campaign.Current.QuestManager.Quests);
                Assert.Equal(gold[instance].Owner + (branch == HeadmanHerdBranch.Deliver ? accepted[Server]._rewardGold : 0),
                    PlayerHero(instance, QuestScenario.ControllerA).Gold);
                Assert.Equal(gold[instance].Other, PlayerHero(instance, QuestScenario.ControllerB).Gold);
                Assert.Equal(branch == HeadmanHerdBranch.Deliver ? 0 : accepted[Server]._animalCountToDeliver,
                    Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
            });
        }
        ClientA.Call(() => Assert.NotEmpty(accepted[ClientA].JournalEntries));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GenericSuccessRequest_IsRefusedWithoutAFullDelivery(int livestock)
    {
        Accept(ClientA);
        ClientA.Call(() =>
        {
            var party = Party(ClientA, QuestScenario.ControllerA);
            party.ItemRoster.AddToCounts(Object<ItemObject>(ClientA, livestockId), -party.ItemRoster.GetItemNumber(Object<ItemObject>(ClientA, livestockId)));
            party.ItemRoster.AddToCounts(Object<ItemObject>(ClientA, livestockId), livestock);
            accepted[ClientA].CompleteQuestWithSuccess();
        });
        environment.FlushCoalescer();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.True(Object<Hero>(Server, scenario.NotableId).Issue.IsSolvingWithQuest));
        ClientA.Call(() => Assert.Contains(accepted[ClientA], Campaign.Current.QuestManager.Quests));
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
    [InlineData(IssueOutcome.QuestCancel, true)]
    [InlineData(IssueOutcome.QuestTimeOut, true)]
    public void CancelAndTimeout_RemoveTheRemainingHerdOnce(IssueOutcome outcome, bool imprisoned = false)
    {
        Accept(ClientA);
        Server.Call(() => Party(Server, QuestScenario.ControllerA).ItemRoster.AddToCounts(Object<ItemObject>(Server, livestockId), -2));
        if (imprisoned)
            Server.Call(() => PlayerHero(Server, QuestScenario.ControllerA).ChangeState(Hero.CharacterStates.Prisoner));
        ClientA.Call(() =>
        {
            if (outcome == IssueOutcome.QuestCancel) accepted[ClientA].CompleteQuestWithCancel();
            else accepted[ClientA].CompleteQuestWithTimeOut();
        });
        environment.FlushCoalescer();

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
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        ClientA.Call(() => Assert.Contains(accepted[ClientA].JournalEntries, log => log.LogText.GetID() == "m41vdhSR"));
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(0, Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GiverDeath_RemovesTheOfferOrCleansUpTheAcceptedHerd(bool takeQuest)
    {
        if (takeQuest) Accept(ClientA);
        Server.Call(() => Campaign.Current.IssueManager.OnHeroKilled(Object<Hero>(Server, scenario.NotableId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(0, Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId)));
            });
        }
    }

    [Fact]
    public void Disconnect_DismissesTheQuestAndRemovesOnlyItsOwnersHerd()
    {
        Accept(ClientA);
        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(ClientA.NetPeer));
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(ClientA.NetPeer, default)));
        Server.PumpGameThread();
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(0, Party(instance, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(instance, livestockId))));
    }

    [Fact]
    public void StartupDismissal_UsesSavedOriginalPartyWithoutOwnership_AndKeepsSurplus()
    {
        Accept(ClientA);
        var data = new CleanupDataStore();
        Server.Call(() =>
        {
            var cleanup = Server.Resolve<IHeadmanHerdCleanup>();
            cleanup.SyncData(data);
            cleanup.SyncData(new CleanupDataStore { IsLoading = true });
            data.IsLoading = true;
            cleanup.SyncData(data);
            Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            Party(Server, QuestScenario.ControllerA).ItemRoster.AddToCounts(Object<ItemObject>(Server, livestockId), 3);
            Party(Server, QuestScenario.ControllerB).ItemRoster.AddToCounts(Object<ItemObject>(Server, livestockId), 7);
            Server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered());
        });
        environment.FlushCoalescer();
        Server.Call(() =>
        {
            Assert.Null(Object<Hero>(Server, scenario.NotableId).Issue);
            Assert.Equal(3, Party(Server, QuestScenario.ControllerA).ItemRoster.GetItemNumber(Object<ItemObject>(Server, livestockId)));
            Assert.Equal(7, Party(Server, QuestScenario.ControllerB).ItemRoster.GetItemNumber(Object<ItemObject>(Server, livestockId)));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanupSaveRoundtrip_PreservesAcceptedHerdPayload(bool takeQuest)
    {
        if (takeQuest) Accept(ClientA);
        Server.Call(() =>
        {
            var issue = Object<Hero>(Server, scenario.NotableId).Issue;
            var cleanup = Server.Resolve<IHeadmanHerdCleanup>();
            if (takeQuest)
            {
                Assert.False(string.IsNullOrEmpty(issue.IssueQuest.StringId));
            }
            var data = new CampaignBehaviorDataStore.BehaviorSaveData(true);
            cleanup.SyncData(data);

            var definitions = new DefinitionContext();
            definitions.FillWithCurrentTypes();
            Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));
            var save = new SaveContext(definitions);
            var metadata = new MetaData();
            Assert.True(save.Save(data, metadata, out var error), error);
            var driver = new InMemDriver();
            Assert.Equal(SaveResult.Success, driver.Save("herd_cleanup", 1, metadata, save.SaveData).GetAwaiter().GetResult().SaveResult);
            var load = new LoadContext(definitions, driver);
            Assert.True(load.Load(driver.Load("herd_cleanup"), loadAsLateInitialize: true));
            var loadedData = Assert.IsType<CampaignBehaviorDataStore.BehaviorSaveData>(load.RootObject);
            Assert.False(loadedData.IsSaving);
            Assert.NotSame(data, loadedData);

            cleanup.SyncData(loadedData);
            Assert.Equal(takeQuest, cleanup.HasRememberedParty(issue));
            var parties = new Dictionary<string, MobileParty>();
            var questIds = new Dictionary<string, string>();
            Assert.True(loadedData.SyncData("coop_herd_cleanup_parties", ref parties));
            Assert.True(loadedData.SyncData("coop_herd_cleanup_quest_issue_ids", ref questIds));
            if (takeQuest)
            {
                var party = Assert.Single(parties).Value;
                Assert.NotSame(Party(Server, QuestScenario.ControllerA), party);
                Assert.Equal(
                    Party(Server, QuestScenario.ControllerA).ItemRoster.Select(element => element.Amount),
                    party.ItemRoster.Select(element => element.Amount));
                Assert.Equal(issue.StringId, Assert.Single(questIds).Value);
                Assert.Equal(issue.IssueQuest.StringId, Assert.Single(questIds).Key);
            }
            else
            {
                Assert.Empty(parties);
                Assert.Empty(questIds);
            }
            ClientB.Call(() =>
            {
                var clientCleanup = ClientB.Resolve<IHeadmanHerdCleanup>();
                clientCleanup.SyncData(loadedData);
                Assert.False(clientCleanup.HasRememberedParty(issue));
            });
        });
    }

    [Fact]
    public void JournalReplay_LeavesALateJoinersUnstartedMirrorAndUnreadMarkersAlone()
    {
        Accept(ClientA);
        Quest mirror = null!;
        var tracker = new ViewDataTrackerCampaignBehavior();
        ClientB.Call(() =>
        {
            using (new AllowedThread())
            {
                mirror = new Quest("herd_late_join", Object<Hero>(ClientB, scenario.NotableId), CampaignTime.Never,
                    accepted[Server]._animalCountToDeliver, Object<ItemObject>(ClientB, livestockId), Object<Settlement>(ClientB, townId),
                    accepted[Server]._rewardGold, Object<Hero>(ClientB, targetId));
                Object<Hero>(ClientB, scenario.NotableId).Issue.IssueQuest = mirror;
            }
            CampaignEvents.QuestLogAddedEvent.AddNonSerializedListener(tracker, tracker.OnQuestLogAdded);
        });
        Server.Call(() => accepted[Server].AddLog(new TextObject("{=herd_owner_reason}owner's quest changed")));
        ClientA.Call(() => Assert.Single(accepted[ClientA].JournalEntries, log => log.LogText.GetID() == "herd_owner_reason"));
        ClientB.Call(() =>
        {
            Assert.Empty(mirror.JournalEntries);
            Assert.Empty(tracker._unExaminedQuestLogs);
        });
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
        var (companionId, troopId) = StartCompanions();

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionGiverDeath_ReturnsTroopsOnce_AndWaitsForAnImprisonedOwner(bool imprisoned)
    {
        var (companionId, troopId) = StartCompanions();
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                if (imprisoned) PlayerHero(Server, QuestScenario.ControllerA).ChangeState(Hero.CharacterStates.Prisoner);
                Object<Hero>(Server, scenario.NotableId).ChangeState(Hero.CharacterStates.Dead);
            }
            Campaign.Current.IssueManager.OnHeroKilled(Object<Hero>(Server, scenario.NotableId), null,
                KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false);
            Assert.Equal(0, Campaign.Current.IssueManager._awaitingAlternativeSolutionTroops.TotalManCount);
            if (imprisoned)
            {
                Assert.True(Object<Hero>(Server, scenario.NotableId).Issue.IsSolvingWithAlternative);
                Object<Hero>(Server, scenario.NotableId).Issue.CompleteIssueWithAlternativeSolution();
                Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
                using (new AllowedThread()) PlayerHero(Server, QuestScenario.ControllerA).ChangeState(Hero.CharacterStates.Active);
                CampaignEventDispatcher.Instance.HourlyTick();
            }
        });
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(40, Party(instance, QuestScenario.ControllerA).MemberRoster.GetTroopCount(Object<CharacterObject>(instance, troopId)));
                Assert.True(Party(instance, QuestScenario.ControllerA).MemberRoster.Contains(Object<Hero>(instance, companionId).CharacterObject));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerB).MemberRoster.GetTroopCount(Object<CharacterObject>(instance, troopId)));
            });
        }
    }

    [Fact]
    public void GiverDeathActionThatDoesNotKill_DoesNotCancelTheCompanionMission()
    {
        StartCompanions();
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                var giver = Object<Hero>(Server, scenario.NotableId);
                giver.ChangeState(Hero.CharacterStates.Dead);
                giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
                KillCharacterAction.ApplyByDeathMarkForced(giver, false);
            }
        });
        environment.FlushCoalescer();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.True(Object<Hero>(Server, scenario.NotableId).Issue.IsSolvingWithAlternative));
    }

    [Fact]
    public void GiverDeathAction_ReturnsCompanionTroopsAndCancelsTheMission()
    {
        var (companionId, troopId) = StartCompanions();
        Server.Call(() =>
        {
            var giver = Object<Hero>(Server, scenario.NotableId);
            giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
            KillCharacterAction.ApplyByDeathMarkForced(giver);
        });
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(40, Party(instance, QuestScenario.ControllerA).MemberRoster.GetTroopCount(Object<CharacterObject>(instance, troopId)));
                Assert.True(Party(instance, QuestScenario.ControllerA).MemberRoster.Contains(Object<Hero>(instance, companionId).CharacterObject));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionEndingAfterOwnerReplacement_ReturnsTroopsToTheCurrentParty(bool giverDies)
    {
        var (companionId, troopId) = StartCompanions();
        var heirId = environment.CreateRegisteredObject<Hero>();
        var replacementPartyId = environment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var heir = Object<Hero>(instance, heirId);
                    heir.Clan = PlayerHero(instance, QuestScenario.ControllerA).Clan;
                    heir._partyBelongedTo = Object<MobileParty>(instance, replacementPartyId);
                    PlayerHero(instance, QuestScenario.ControllerA)._partyBelongedTo = null;
                }
            });
        }
        Server.Call(() =>
        {
            var player = players.Single(player => player.ControllerId == QuestScenario.ControllerA);
            Assert.True(Server.Resolve<IPlayerManager>().ReplacePlayer(player,
                new Player(player.ControllerId, heirId, replacementPartyId, player.ClanId,
                    player.CharacterObjectId, player.OriginalClanId)));
            var issue = Object<Hero>(Server, scenario.NotableId).Issue;
            Assert.True(Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(issue, out var owner));
            Assert.Same(Object<Hero>(Server, heirId), owner);
            Object<MobileParty>(Server, replacementPartyId).MemberRoster.Clear();
            if (giverDies)
            {
                var giver = issue.IssueOwner;
                giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
                KillCharacterAction.ApplyByDeathMarkForced(giver);
            }
            else
            {
                issue._failureChance = -1f;
                issue.CompleteIssueWithAlternativeSolution();
            }
            Assert.Empty(issue.AlternativeSolutionSentTroops.GetTroopRoster());
            Assert.Equal(Hero.CharacterStates.Active, Object<Hero>(Server, companionId).HeroState);
        });
        environment.FlushCoalescer();

        Assert.Equal(giverDies ? IssueOutcome.QuestCancel : IssueOutcome.AlternativeSolution,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var troop = Object<CharacterObject>(instance, troopId);
                var companion = Object<Hero>(instance, companionId);
                var currentParty = Object<MobileParty>(instance, replacementPartyId);
                Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(40, currentParty.MemberRoster.GetTroopCount(troop));
                Assert.Equal(1, currentParty.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerA).MemberRoster.GetTroopCount(troop));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerA).MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerB).MemberRoster.GetTroopCount(troop));
                Assert.Equal(0, Party(instance, QuestScenario.ControllerB).MemberRoster.GetTroopCount(companion.CharacterObject));
            });
        }
    }

    [Fact]
    public void ReturningAlternativeTroopsTwice_AddsThemOnlyOnce()
    {
        var (_, troopId) = StartCompanions();
        Server.Call(() =>
        {
            using var scope = OwnerScope(Server, QuestScenario.ControllerA);
            var issue = Object<Hero>(Server, scenario.NotableId).Issue;
            var cleanup = Server.Resolve<IHeadmanHerdCleanup>();
            Assert.True(cleanup.ReturnAlternativeTroops(issue));
            var first = Party(Server, QuestScenario.ControllerA).MemberRoster.GetTroopCount(Object<CharacterObject>(Server, troopId));
            Assert.True(cleanup.ReturnAlternativeTroops(issue));
            var second = Party(Server, QuestScenario.ControllerA).MemberRoster.GetTroopCount(Object<CharacterObject>(Server, troopId));
            Assert.Equal(first, second);
            Assert.Empty(issue.AlternativeSolutionSentTroops.GetTroopRoster());
        });
    }

    [Fact]
    public void GiverDeathAction_CancelsPersonalQuestOnOwnerClient()
    {
        Accept(ClientA);
        Server.Call(() =>
        {
            var giver = Object<Hero>(Server, scenario.NotableId);
            giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
            KillCharacterAction.ApplyByDeathMarkForced(giver);
        });
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        ClientA.Call(() =>
        {
            Assert.Empty(Campaign.Current.QuestManager.Quests);
            Assert.Null(Object<Hero>(ClientA, scenario.NotableId).Issue);
        });
    }

    [Fact]
    public void GiverDeathAfterOwnerRemoval_StillCancelsTheIssue()
    {
        Accept(ClientA);
        Server.Call(() =>
        {
            Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            Campaign.Current.IssueManager.OnHeroKilled(Object<Hero>(Server, scenario.NotableId), null,
                KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false);
        });
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        ClientA.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    internal (string CompanionId, string TroopId) StartCompanions()
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
                    issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                }
                AlternativeSolutionPickerDriver.Commit(ClientA, issue, Object<Hero>(ClientA, scenario.OwnerHeroId),
                    Party(ClientA, QuestScenario.ControllerA), troop, 40);
                issue.StartIssueWithAlternativeSolution();
            }
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        return (companionId, troopId);
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
            item.ItemCategory = GameObjectCreator.CreateInitializedObject<ItemCategory>();
            MBObjectManager.Instance.RegisterObject(item);
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
        environment.FlushCoalescer();
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

    private Hero PlayerHero(EnvironmentInstance instance, string controller)
        => Object<Hero>(instance, players.Single(player => player.ControllerId == controller).HeroId);

    private MainHeroSubstitutionScope OwnerScope(EnvironmentInstance instance, string controller)
        => new(Object<Hero>(instance, players.Single(player => player.ControllerId == controller).HeroId), Party(instance, controller));

    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private sealed class CleanupDataStore : IDataStore
    {
        private Dictionary<string, MobileParty>? savedParties;
        private Dictionary<string, string>? savedQuestIssueIds;
        public bool IsLoading { get; set; }
        public bool IsSaving => !IsLoading;

        public bool SyncData<T>(string key, ref T data)
        {
            if (key == "coop_herd_cleanup_parties")
            {
                if (IsLoading) data = (T)(object)(savedParties ?? new Dictionary<string, MobileParty>());
                else savedParties = new Dictionary<string, MobileParty>((Dictionary<string, MobileParty>)(object)data!);
            }
            else if (key == "coop_herd_cleanup_quest_issue_ids")
            {
                if (IsLoading) data = (T)(object)(savedQuestIssueIds ?? new Dictionary<string, string>());
                else savedQuestIssueIds = new Dictionary<string, string>((Dictionary<string, string>)(object)data!);
            }
            return true;
        }
    }
}
