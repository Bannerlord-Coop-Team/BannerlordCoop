using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Players;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Actions;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises the real issue through the framework with two independent player clients.</summary>
public class ExtortionByDesertersTests : IDisposable
{
    private static readonly MethodBase[] TextLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
        AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLinkWithName)),
        AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.EncyclopediaLinkWithName)),
    };
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly MethodCallRecorder creation;
    private readonly string villageId;
    private readonly string townId;
    private readonly string townComponentId;
    private readonly string settlementId;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public ExtortionByDesertersTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        // Navigation and party templates need the native map, the framework path does not.
        creation = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty)));
        villageId = environment.CreateRegisteredObject<Village>();
        townId = environment.CreateRegisteredObject<Settlement>();
        townComponentId = environment.CreateRegisteredObject<Town>();
        settlementId = environment.CreateRegisteredObject<Settlement>();
        scenario = new QuestScenario(environment, notable => new Issue(notable), Prepare);
    }

    public void Dispose()
    {
        creation.Dispose();
        environment.Dispose();
    }

    private void Prepare(EnvironmentInstance instance, Hero notable)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townId, out var town));
        Assert.True(instance.ObjectManager.TryGetObject<Town>(townComponentId, out var townComponent));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
        village.Bound = town;
        town.SetSettlementComponent(townComponent);
        town.Town.Security = 30;
        settlement.SetSettlementComponent(village);
        notable.StayingInSettlement = settlement;
        notable.Occupation = Occupation.Headman;
    }

    private Quest Accept()
    {
        scenario.Accept(Owner, TextLinks);
        var quest = (Quest)scenario.QuestOf(Owner);
        Owner.Call(() => quest.StartQuest());
        return quest;
    }

    [Fact]
    public void Accept_CreatesWorldPartyOnlyOnServerAndPersonalQuestOnlyOnOwner()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Assert.Equal(1, creation.CountFor(Server));
        Assert.Equal(0, creation.CountFor(Owner));
        Assert.Equal(0, creation.CountFor(Other));
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
            Assert.Empty(notable.Issue.JournalEntries);
        });
    }

    [Fact]
    public void State_BindsExistingPartyAndProgressOnlyToOwner()
    {
        var quest = Accept();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var state = new NetworkExtortionByDesertersState(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, partyId, "", (int)State.DesertersAreDefeated, "deserters defeated", false);
        Owner.SimulateMessage(this, state);
        Other.SimulateMessage(this, state);
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, quest._deserterMobileParty);
            Assert.Equal(State.DesertersAreDefeated, quest._currentState);
            Assert.True(quest.IsTracked(quest.QuestGiver));
            Assert.True(quest.IsTracked(quest.QuestSettlement));
            Assert.Contains(quest.JournalEntries, log => log.LogText.ToString() == "deserters defeated");
        });
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
            Assert.Empty(notable.Issue.JournalEntries);
        });
    }

    [Fact]
    public void WarDeclaredOnTheServer_ReachesTheQuestOnce()
    {
        Accept();
        var firstId = environment.CreateRegisteredObject<Kingdom>();
        var secondId = environment.CreateRegisteredObject<Kingdom>();
        using var calls = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared)));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(firstId, out var first));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(secondId, out var second));
            CampaignEventDispatcher.Instance.OnWarDeclared(first, second, DeclareWarAction.DeclareWarDetail.Default);
        }, TextLinks);
        Assert.Equal(1, calls.CountFor(Server));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RewardChoice_RequestsItsBranchAndServerCompletesIt(byte branch)
    {
        var quest = Accept();
        var mirror = (Quest)scenario.QuestOf(Server);
        Server.Call(() => mirror._currentState = State.DesertersAreDefeated);
        using var result = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult)));
        Owner.Call(() =>
        {
            ChooseReward(quest, branch - 1);
        }, TextLinks);
        Assert.Equal(branch, Assert.Single(Owner.NetworkSentMessages.GetMessages<RequestQuestBranch>()).Proof);
        Assert.Equal(1, result.CountFor(Server));
        Assert.Equal(0, result.CountFor(Owner));
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.True(quest.IsFinalized);
    }

    [Fact]
    public void RewardChoice_BeforeVictoryHasNoRewardOrCompletion()
    {
        var quest = Accept();
        using var result = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult)));
        Owner.Call(() => ChooseReward(quest, 0), TextLinks);
        Assert.Equal(0, result.CountFor(Server));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.True(quest.IsOngoing);
    }

    [Fact]
    public void ClientIssueLifecycle_DoesNotPurgeAnIssueWhenSecurityChanges()
    {
        var otherHeroId = string.Empty;
        var otherPartyId = string.Empty;
        Server.Call(() =>
        {
            var player = Server.Resolve<IPlayerManager>().Players
                .Single(player => player.ControllerId == QuestScenario.ControllerB);
            otherHeroId = player.HeroId;
            otherPartyId = player.MobilePartyId;
        });
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));
            Assert.True(Other.ObjectManager.TryGetObject<Town>(townComponentId, out var town));
            Assert.True(Other.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var party));
            var settlement = notable.CurrentSettlement;
            Assert.NotNull(settlement);
            var previousSettlement = party.CurrentSettlement;
            party.CurrentSettlement = settlement;
            try
            {
                using (new MainHeroSubstitutionScope(otherHero, party))
                using (new AllowedThread())
                {
                    town.Security = 90;
                    Campaign.Current.IssueManager.OnSettlementEntered(party, settlement, otherHero);
                    Campaign.Current.IssueManager.DailyTick();
                }
            }
            finally
            {
                party.CurrentSettlement = previousSettlement;
            }
            Assert.NotNull(notable.Issue);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void UnknownBranch_IsRejected(byte proof)
    {
        Accept();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
            Assert.False(descriptor.FinalizationProofStrategy.TryRunBranch(scenario.QuestOf(Server), proof));
        });
    }

    [Fact]
    public void RaidReplayOnOwner_DoesNotApplyConsequences()
    {
        var quest = Accept();
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Village>(villageId, out var village));
            using (new AllowedThread()) quest.OnVillageBeingRaided(village);
            Assert.True(quest.IsOngoing);
        });
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(nameof(Quest.CreateDeserterParty))]
    [InlineData(nameof(Quest.CreateDefenderParty))]
    [InlineData(nameof(Quest.DestroyDeserterParty))]
    [InlineData(nameof(Quest.DestroyDefenderParty))]
    [InlineData(nameof(Quest.ReleaseDeserterParty))]
    [InlineData(nameof(Quest.HourlyTick))]
    [InlineData(nameof(Quest.GameMenuOptionSelected))]
    [InlineData(nameof(Quest.OnWarDeclared))]
    [InlineData(nameof(Quest.OnClanChangedKingdom))]
    [InlineData(nameof(Quest.OnMapEventStarted))]
    [InlineData(nameof(Quest.OnSettlementLeft))]
    public void WorldMethod_IsBlockedEvenDuringOwnerReplay(string methodName)
    {
        var quest = Accept();
        var method = AccessTools.Method(typeof(Quest), methodName);
        // The constructor recorder already covers deserter creation.
        if (methodName == nameof(Quest.CreateDeserterParty))
        {
            creation.Clear();
            Owner.Call(() => { using (new AllowedThread()) quest.CreateDeserterParty(); });
            Assert.Equal(0, creation.CountFor(Owner));
            return;
        }

        using var recorder = new MethodCallRecorder(Priority.Last, method);
        var arguments = method.GetParameters().Select(parameter => parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray();
        Owner.Call(() => { using (new AllowedThread()) method.Invoke(quest, arguments); });
        Assert.Equal(0, recorder.CountFor(Owner));
    }

    [Fact]
    public void SeparatePlayers_CanHoldSeparateDeserterQuests()
    {
        Accept();
        var secondId = AddSecondIssue();
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(secondId, out var notable));
            notable.Issue.StartIssueWithQuest();
            notable.Issue.IssueQuest.StartQuest();
        }, TextLinks);
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondId, "second_deserters", out var owner));
            Assert.Equal(QuestScenario.ControllerB, owner);
        });
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(secondId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
        });
    }

    [Fact]
    public void SamePlayer_CannotTakeAnotherDeserterQuest()
    {
        Accept();
        var secondId = AddSecondIssue();
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(secondId, out var notable));
            notable.Issue.StartIssueWithQuest();
        }, TextLinks);
        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondId, "second_deserters", out _));
        });
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    [Fact]
    public void Victory_RemainsReadyForHandInAfterTheDeserterPartyIsGone()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        Server.Call(() =>
        {
            quest._currentState = State.DesertersAreDefeated;
            quest._deserterBattleFinalizedForTheFirstTime = true;
            Assert.True(Server.Resolve<ExtortionByDesertersFinalizationProofStrategy>()
                .TryRunBranch(quest, (byte)ExtortionByDesertersBranch.BattleCleanup));
            quest.HourlyTick();
            Assert.True(quest.IsOngoing);
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Owner.Call(() => ChooseReward((Quest)scenario.QuestOf(Owner), 0), TextLinks);
        Assert.Equal(IssueOutcome.QuestSuccess,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Theory]
    [InlineData(0, 1f, 0, 4, 5, 0)]
    [InlineData(1, 0.5f, 1, 6, 10, 25)]
    [InlineData(2, 0f, 2, 8, 15, 100)]
    public void DialogueReward_AppliesTheDistinctEffectsToTheOwnerOnce(
        int choice, float goldMultiplier, int renown, int relation, int power, int prosperity)
    {
        var quest = Accept();
        var serverQuest = (Quest)scenario.QuestOf(Server);
        Hero ownerHero = null!;
        Hero otherHero = null!;
        Hero giver = null!;
        Town town = null!;
        var goldBefore = 0;
        var otherGoldBefore = 0;
        float renownBefore = 0, powerBefore = 0, prosperityBefore = 0, securityBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerB, out var other));
            Assert.True(Server.ObjectManager.TryGetObject(owner.HeroId, out ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject(other.HeroId, out otherHero));
            Assert.True(Server.ObjectManager.TryGetObject(scenario.NotableId, out giver));
            Assert.True(Server.ObjectManager.TryGetObject(townComponentId, out town));
            goldBefore = ownerHero.Gold;
            otherGoldBefore = otherHero.Gold;
            renownBefore = ownerHero.Clan.Renown;
            powerBefore = giver.Power;
            prosperityBefore = town.Prosperity;
            securityBefore = town.Security;
            serverQuest._currentState = State.DesertersAreDefeated;
        });
        Owner.Call(() =>
        {
            quest._currentState = State.DesertersAreDefeated;
            ChooseReward(quest, choice);
        }, TextLinks);
        Server.Call(() =>
        {
            Assert.Equal(goldBefore + (int)(serverQuest.RewardGold * goldMultiplier), ownerHero.Gold);
            Assert.Equal(otherGoldBefore, otherHero.Gold);
            Assert.Equal(renownBefore + renown, ownerHero.Clan.Renown);
            Assert.Equal(powerBefore + power, giver.Power);
            Assert.Equal(prosperityBefore + prosperity, town.Prosperity);
            Assert.Equal(securityBefore + 10, town.Security);
            Assert.Equal(relation, serverQuest.RelationshipChangeWithQuestGiver);
        });
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.True(quest.IsFinalized);
    }

    private static void ChooseReward(Quest quest, int choice)
    {
        var consequences = quest.QuestCompletionDialogFlow.Lines
            .Where(line => line.ConsequenceDelegate != null).ToArray();
        Assert.Equal(3, consequences.Length);
        consequences[choice].ConsequenceDelegate();
    }

    [Fact]
    public void Notification_IsShownOnlyToOwnerWithoutAddingAJournalEntry()
    {
        var quest = Accept();
        var entries = quest.JournalEntries.Count;
        using var notifications = new MethodCallRecorder(AccessTools.GetDeclaredMethods(typeof(MBInformationManager))
            .Where(method => method.Name == nameof(MBInformationManager.AddQuickInformation)).ToArray());
        var state = new NetworkExtortionByDesertersState(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, "", "", (int)State.DesertersRunningAwayFromPlayer, "", false,
            (byte)ExtortionByDesertersNotice.DesertersNoticedPlayer);
        Owner.SimulateMessage(this, state);
        Other.SimulateMessage(this, state);
        Assert.Equal(1, notifications.CountFor(Owner));
        Assert.Equal(0, notifications.CountFor(Other));
        Assert.Equal(entries, quest.JournalEntries.Count);
    }

    [Fact]
    public void ServerBattleStarted_DrivesTheAmbushWithoutAClientBattleEvent()
    {
        Accept();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var quest = (Quest)scenario.QuestOf(Server);
        using var branches = new MethodCallRecorder(AccessTools.Method(typeof(ExtortionByDesertersFinalizationProofStrategy),
            nameof(ExtortionByDesertersFinalizationProofStrategy.TryRunBranch)));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            quest._deserterMobileParty = party;
            CampaignEventDispatcher.Instance.OnStartBattle(party.Party, quest.QuestSettlement.Party, null, false);
        });
        Assert.Equal(1, branches.CountFor(Server));
        Assert.Equal(0, branches.CountFor(Owner));
    }

    [Fact]
    public void ServerAmbush_AssignsOwnerAndCreatedDefendersAndRemovesMilitia()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        using var encounter = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(Quest), nameof(Quest.StartAmbushEncounter)));
        var disabled = TextLinks.Concat(new[]
        {
            AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
            AccessTools.Method(typeof(MobilePartyHelper), nameof(MobilePartyHelper.FillPartyManuallyAfterCreation)),
        }).ToArray();
        MobileParty ownerParty = null!;
        MobileParty militia = null!;
        MapEvent raid = null!;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject(owner.MobilePartyId, out ownerParty));
            quest.QuestSettlement.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            ownerParty.CurrentSettlement = quest.QuestSettlement;
            var component = new MilitiaPartyComponent(quest.QuestSettlement, new MilitiaPartyComponent.InitializationArgs());
            militia = MobileParty.CreateParty("extortion_test_militia", component);
            quest.QuestSettlement.MilitiaPartyComponent = component;
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            deserters.SetPartyUsedByQuest(true);
            raid = CreateBattle(deserters, quest.QuestSettlement.Party, true);
            militia.MapEventSide = raid.DefenderSide;
            CampaignEventDispatcher.Instance.OnStartBattle(deserters.Party, quest.QuestSettlement.Party, null, false);
            Assert.NotNull(quest._defenderMobileParty);
            Assert.True(quest._defenderMobileParty.IsCurrentlyUsedByAQuest);
            Assert.True(Server.ObjectManager.TryGetId(quest._defenderMobileParty, out _));
            Assert.Same(raid.DefenderSide, quest._defenderMobileParty.MapEventSide);
            Assert.Same(raid.DefenderSide, ownerParty.MapEventSide);
            Assert.Null(militia.MapEventSide);
            Assert.False(Server.Resolve<ExtortionByDesertersFinalizationProofStrategy>()
                .TryRunBranch(quest, (byte)ExtortionByDesertersBranch.Ambush));
        }, disabled);
        Assert.Equal(1, encounter.CountFor(Owner));
        Assert.Equal(0, encounter.CountFor(Other));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkExtortionByDesertersState>(), state => state.Ambush);
    }

    [Fact]
    public void AmbushHandoff_PreservesAnEncounterAlreadyAttachedToTheReplicatedRaid()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        var ownerQuest = (Quest)scenario.QuestOf(Owner);
        string raidId = null!, ownerPartyId = null!, ownerHeroId = null!;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            ownerPartyId = owner.MobilePartyId;
            ownerHeroId = owner.HeroId;
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            ownerParty.CurrentSettlement = quest.QuestSettlement;
            var raid = CreateBattle(deserters, quest.QuestSettlement.Party, true);
            raid.MapEventSettlement = quest.QuestSettlement;
            defenders.MapEventSide = raid.DefenderSide;
            ownerParty.MapEventSide = raid.DefenderSide;
            Assert.True(Server.ObjectManager.TryGetId(raid, out raidId));
            Server.Resolve<ExtortionByDesertersSync>().Send(quest);
        }, TextLinks.Append(AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize))));
        using var finish = new MethodCallRecorder(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)));
        using var menus = new MethodCallRecorder(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)));
        using var conversations = new MethodCallRecorder(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            using var playerScope = new MainHeroSubstitutionScope(ownerHero, ownerParty);
            Assert.True(Owner.ObjectManager.TryGetObject<MapEvent>(raidId, out var raid));
            // Seed the already-applied raid graph at the client handoff boundary.
            using (new AllowedThread())
            {
                raid._mapEventType = MapEvent.BattleTypes.Raid;
                raid.MapEventSettlement = ownerQuest.QuestSettlement;
                ownerQuest._deserterMobileParty.MapEventSide = raid.AttackerSide;
                ownerQuest._defenderMobileParty.MapEventSide = raid.DefenderSide;
                ownerParty.MapEventSide = raid.DefenderSide;
            }
            Assert.Same(raid, MobileParty.MainParty.MapEvent);
            PlayerEncounter.Start();
            var existing = PlayerEncounter.Current;
            existing._mapEvent = raid;
            existing.FirstInit = false;
            var participants = raid.InvolvedParties.ToArray();
            Assert.Contains(participants, party => party.MobileParty == ownerQuest._defenderMobileParty);
            try
            {
                ownerQuest.StartAmbushEncounter();
                Assert.Equal(0, finish.CountFor(Owner));
                Assert.Same(existing, PlayerEncounter.Current);
                Assert.Same(raid, PlayerEncounter.Battle);
                Assert.False(existing.FirstInit);
                Assert.False(raid.IsFinalized);
                Assert.Equal(participants, raid.InvolvedParties.ToArray());
                Assert.Same(raid.DefenderSide, MobileParty.MainParty.MapEventSide);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });
        Assert.Equal(new[] { "encounter_meeting" }, menus.MenusFor(Owner));
        Assert.Equal(1, conversations.CountFor(Owner));
        Assert.Equal(0, conversations.CountFor(Other));
    }

    [Fact]
    public void CoopFinalizeAux_CleansQuestPartiesWithoutAnEncounterMenuRequest()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        using var destruction = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));
        Server.Call(() =>
        {
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            deserters.SetPartyUsedByQuest(true);
            var battle = CreateBattle(deserters, defenders.Party, false);
            // Battle results were committed before the co-op finalizer tears down the event.
            quest._currentState = State.DesertersAreDefeated;
            quest._deserterBattleFinalizedForTheFirstTime = true;
            battle.FinalizeEventAux();
            Assert.True(battle.IsFinalized);
            Assert.Null(quest._deserterMobileParty);
            Assert.Null(quest._defenderMobileParty);
            Assert.False(quest._deserterBattleFinalizedForTheFirstTime);
            Assert.True(quest.IsOngoing);
            battle.FinalizeEventAux();
        }, TextLinks.Append(AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize))));
        Assert.Equal(2, destruction.CountFor(Server));
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Owner.Call(() =>
        {
            var ownerQuest = (Quest)scenario.QuestOf(Owner);
            Assert.Null(ownerQuest._deserterMobileParty);
            Assert.Null(ownerQuest._defenderMobileParty);
            Assert.Equal(State.DesertersAreDefeated, ownerQuest._currentState);
        });
    }

    [Fact]
    public void RaidRepelledWithoutTheOwner_KeepsTheQuestForTheSurvivingDeserters()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        Server.Call(() =>
        {
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            deserters.SetPartyUsedByQuest(true);
            var raid = CreateBattle(deserters, quest.QuestSettlement.Party, true);
            raid._battleState = BattleState.DefenderVictory;
            CampaignEventDispatcher.Instance.OnMapEventEnded(raid);
            Assert.True(quest.IsOngoing);
            Assert.Equal(State.DesertersMovingToSettlement, quest._currentState);
        }, TextLinks.Append(AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize))));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void BattleWithoutTheOwnerThatDestroysTheDeserters_CancelsTheQuest()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        Server.Call(() =>
        {
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var victors = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            deserters.SetPartyUsedByQuest(true);
            var battle = CreateBattle(deserters, victors.Party, false);
            battle._battleState = BattleState.DefenderVictory;
            DestroyPartyAction.Apply(null, deserters);
            battle.FinalizeEventAux();
            Assert.False(deserters.IsActive);
            Assert.False(quest.IsOngoing);
        }, TextLinks.Concat(new[]
        {
            AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
            AccessTools.Method(typeof(MapEvent), nameof(MapEvent.MovePartyToSuitablePositionOnMapEventFinalize)),
        }).ToArray());
        Assert.Equal(IssueOutcome.QuestCancel,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private static MapEvent CreateBattle(MobileParty attacker, PartyBase defender, bool raid)
    {
        var battle = GameObjectCreator.CreateInitializedObject<MapEvent>();
        battle.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
        battle.Initialize(attacker.Party, defender, new FieldBattleEventComponent(battle), MapEvent.BattleTypes.FieldBattle);
        battle.MapEventVisual = null;
        battle._mapEventType = raid ? MapEvent.BattleTypes.Raid : MapEvent.BattleTypes.FieldBattle;
        return battle;
    }

    [Fact]
    public void OwnerLeavingVillageBeforeRaid_ReticksFlightWithoutWaitingForAnHour()
    {
        Accept();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var quest = (Quest)scenario.QuestOf(Server);
        using var flight = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.HandleDesertersRunningAway)));
        using var notifications = new MethodCallRecorder(AccessTools.GetDeclaredMethods(typeof(MBInformationManager))
            .Where(method => method.Name == nameof(MBInformationManager.AddQuickInformation)).ToArray());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerB, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(owner.MobilePartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(other.MobilePartyId, out var otherParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var deserters));
            quest._deserterMobileParty = deserters;
            deserters.Position = ownerParty.Position;
            ownerParty.CurrentSettlement = null;
            CampaignEventDispatcher.Instance.OnSettlementLeft(otherParty, quest.QuestSettlement);
            Assert.Equal(State.DesertersMovingToSettlement, quest._currentState);
            CampaignEventDispatcher.Instance.OnSettlementLeft(ownerParty, quest.QuestSettlement);
            Assert.Equal(State.DesertersRunningAwayFromPlayer, quest._currentState);
            Assert.True(quest.IsOngoing);
        });
        Assert.Equal(1, flight.CountFor(Server));
        Assert.Equal(1, notifications.CountFor(Owner));
        Assert.Equal(0, notifications.CountFor(Other));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void Timeout_AppliesThePenaltyAndJournalOnTheOwningQuest()
    {
        var quest = Accept();
        var mirror = (Quest)scenario.QuestOf(Server);
        Owner.Call(() => quest.CompleteQuestWithTimeOut(), TextLinks);
        Assert.Equal(-10, mirror.RelationshipChangeWithQuestGiver);
        Assert.True(quest.IsFinalized);
        Assert.Single(quest.JournalEntries);
        Assert.Equal(IssueOutcome.QuestTimeOut,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Other.Call(() => Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, item => item is Quest));
    }

    [Fact]
    public void CapturedOwner_DoesNotUseAnotherPlayersPartyForFlight()
    {
        Accept();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var quest = (Quest)scenario.QuestOf(Server);
        using var flight = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.HandleDesertersRunningAway)));
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var deserters));
            using (new AllowedThread())
            {
                hero._partyBelongedTo = null;
                hero.ChangeState(Hero.CharacterStates.Prisoner);
            }
            quest._deserterMobileParty = deserters;
            using (new MainHeroSubstitutionScope(hero, null)) quest.HourlyTick();
            Assert.Equal(State.DesertersMovingToSettlement, quest._currentState);
        });
        Assert.Equal(0, flight.Count);
    }

    [Fact]
    public void OwnerLeaving_ReleasesTheDesertersAndRemovesTheDefendersOfTheDismissedQuest()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        using var destruction = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));
        MobileParty deserters = null!;
        Server.Call(() =>
        {
            deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            deserters.SetPartyUsedByQuest(true);
            Server.Resolve<IPlayerManager>().ClearPeer(Owner.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Owner.NetPeer, default(DisconnectInfo)));
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.Null(notable.Issue);
            Assert.False(deserters.IsCurrentlyUsedByAQuest);
            Assert.Null(quest._defenderMobileParty);
        });
        Assert.Equal(1, destruction.CountFor(Server));
        Assert.Equal(IssueOutcome.QuestCancel,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private string AddSecondIssue()
    {
        var secondId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(secondId, out var notable));
                using (new AllowedThread())
                {
                    Prepare(instance, notable);
                    var issue = new Issue(notable) { StringId = "second_deserters" };
                    Campaign.Current.IssueManager._issues.Add(notable, issue);
                    notable.OnIssueCreatedForHero(issue);
                }
            });
        }
        return secondId;
    }
}
