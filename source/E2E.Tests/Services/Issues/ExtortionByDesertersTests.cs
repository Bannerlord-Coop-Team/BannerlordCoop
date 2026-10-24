using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.AutoSync;
using GameInterface.CoopSessionData;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.Players;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Utils.NetworkEvents;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
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
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
    private readonly ITestOutputHelper output;
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
        this.output = output;
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

    private Quest Accept(IEnumerable<MethodBase>? disabledMethods = null)
    {
        scenario.Accept(Owner, disabledMethods ?? TextLinks);
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
        var text = new TextObject("{=YjxCkglX}You've failed to complete this quest in time. Your agreement with {QUEST_GIVER.LINK} was canceled.");
        var properties = TextObject.GetEmpty();
        properties.SetTextVariable("LINK", new TextObject("{=!}Danustica headman"));
        text.SetTextVariable("QUEST_GIVER", properties);
        var state = new NetworkExtortionByDesertersState(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, partyId, "", (int)State.DesertersAreDefeated, text, false);
        var wire = Server.SerializeForWire(state);
        Owner.SimulateMessage(this, wire);
        Other.SimulateMessage(this, wire);
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, quest._deserterMobileParty);
            Assert.Equal(State.DesertersAreDefeated, quest._currentState);
            Assert.True(quest.IsTracked(quest.QuestGiver));
            Assert.True(quest.IsTracked(quest.QuestSettlement));
            var received = Assert.Single(quest.JournalEntries).LogText;
            Assert.Equal(text.Value, received.Value);
            var giver = Assert.IsType<TextObject>(received.Attributes["QUEST_GIVER"]);
            Assert.Equal("{=!}Danustica headman", Assert.IsType<TextObject>(giver.Attributes["LINK"]).Value);
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
    [InlineData(0, IssueOutcome.QuestFail, -10)]
    [InlineData(1, IssueOutcome.QuestFail, -5)]
    [InlineData(2, IssueOutcome.QuestCancel, 0)]
    public void VillageRaided_EndsTheOwnersQuestWithTheRaidersConsequences(
        int attacker, IssueOutcome outcome, int relationChange)
    {
        var ownerQuest = Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        var log = string.Empty;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OtherPartyId, out var otherParty));
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            deserters.SetPartyUsedByQuest(true);
            deserters.Aggressiveness = 0f;
            deserters.MemberRoster.AddToCounts(deserters.LeaderHero.CharacterObject, 1);
            ownerParty.CurrentSettlement = null;
            var raider = attacker == 0 ? deserters : attacker == 1 ? ownerParty : otherParty;
            // Seed the raid graph without announcing an unrelated field-battle start.
            var raid = GameObjectCreator.CreateInitializedObject<MapEvent>();
            raid.MapEventVisual = null;
            raid._mapEventType = MapEvent.BattleTypes.Raid;
            raid.MapEventSettlement = quest.QuestSettlement;
            raid._sides[(int)BattleSideEnum.Attacker] = new MapEventSide(raid, BattleSideEnum.Attacker, raider.Party);
            raid._sides[(int)BattleSideEnum.Defender] = new MapEventSide(raid, BattleSideEnum.Defender, quest.QuestSettlement.Party);
            raider.MapEventSide = raid.AttackerSide;
            quest.QuestSettlement.Party.MapEventSide = raid.DefenderSide;
            log = (attacker == 0 ? quest.OnQuestFailed2LogText :
                attacker == 1 ? quest.OnQuestFailed3LogText : quest.OnQuestCancel2LogText).Value;

            AssertQuestPenalty(quest, relationChange,
                () => CampaignEventDispatcher.Instance.OnVillageBeingRaided(quest.QuestSettlement.Village));

            Assert.True(deserters.IsActive);
            Assert.False(deserters.IsCurrentlyUsedByAQuest);
            Assert.Equal(1f, deserters.Aggressiveness);
            Assert.Same(deserters, quest._deserterMobileParty);
            Assert.False(defenders.IsActive);
            Assert.Null(quest._defenderMobileParty);
            Assert.True(ownerParty.IsActive);
            Assert.True(otherParty.IsActive);
        }, TextLinks.Append(AccessTools.Method(typeof(MapEvent), nameof(MapEvent.MovePartyToSuitablePositionOnMapEventFinalize))));
        AssertQuestEnding(ownerQuest, outcome, log);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RunningDeserters_LostSightTakesPriorityOverTheTenHourDeadline(bool lostSight, bool timedOut)
    {
        var ownerQuest = Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        var log = string.Empty;
        // The disabled SeeingRange getter pins sight at zero without the native map.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            deserters.SetPartyUsedByQuest(true);
            deserters.Aggressiveness = 0f;
            quest._currentState = State.DesertersRunningAwayFromPlayer;
            // The harness stubs HoursFromNow and CampaignTime.Now to zero; build the deadline from the real map time.
            quest._desertersRunAwayTimeoutTime = Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(10f);
            if (timedOut) Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Hours(11f).NumTicks;
            Assert.Equal(timedOut, quest._desertersRunAwayTimeoutTime.IsPast);
            ownerParty._position = new CampaignVec2(Vec2.Zero, true);
            deserters._position = new CampaignVec2(new Vec2(lostSight ? 10f : 0f, 0f), true);
            Assert.Equal(lostSight, deserters.Position.Distance(ownerParty.Position) > ownerParty.SeeingRange + 3f);
            log = quest.OnQuestFailed1LogText.Value;

            AssertQuestPenalty(quest, -10, () =>
            {
                using (new MainHeroSubstitutionScope(ownerHero, ownerParty)) quest.TickDesertersPartyLogic();
            });

            Assert.False(defenders.IsActive);
            Assert.Null(quest._defenderMobileParty);
            Assert.Equal(lostSight, deserters.IsActive);
            if (lostSight)
            {
                Assert.Same(deserters, quest._deserterMobileParty);
                Assert.False(deserters.IsCurrentlyUsedByAQuest);
                Assert.Equal(1f, deserters.Aggressiveness);
            }
            else Assert.Null(quest._deserterMobileParty);
        }, TextLinks.Append(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.SeeingRange))));
        AssertQuestEnding(ownerQuest, IssueOutcome.QuestFail, log);
    }

    private void AssertQuestPenalty(Quest quest, int relationChange, Action callback)
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var ownerHero));
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.OtherHeroId, out var otherHero));
        var giver = quest.QuestGiver;
        var town = quest.QuestSettlement.Village.Bound.Town;
        giver.AddPower(100);
        town.Prosperity = 1000;
        var power = giver.Power;
        var security = town.Security;
        var prosperity = town.Prosperity;
        var ownerRelation = giver.GetRelation(ownerHero);
        var otherRelation = giver.GetRelation(otherHero);

        var issues = new IssuesCampaignBehavior();
        issues.RegisterEvents();
        try
        {
            callback();
        }
        finally
        {
            CampaignEvents.Instance.RemoveListeners(issues);
        }

        Assert.False(quest.IsOngoing);
        Assert.Equal(relationChange, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(ownerRelation + relationChange, giver.GetRelation(ownerHero));
        Assert.Equal(otherRelation, giver.GetRelation(otherHero));
        Assert.Equal(power + (relationChange == 0 ? 0 : -10), giver.Power);
        Assert.Equal(security + (relationChange == 0 ? 0 : -10), town.Security);
        Assert.Equal(prosperity + (relationChange == 0 ? 0 : -50), town.Prosperity);
    }

    private void AssertQuestEnding(Quest ownerQuest, IssueOutcome outcome, string log)
    {
        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        var state = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkExtortionByDesertersState>(),
            state => state.Log?.Value == log);
        Assert.Equal(QuestScenario.ControllerA, state.ControllerId);
        Assert.Single(Owner.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(), state => state.Log?.Value == log);
        Assert.DoesNotContain(Other.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(), state => state.Log?.Value == log);
        Owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Single(ownerQuest.JournalEntries, entry => entry.LogText?.Value == log);
        });
        Other.Call(() => Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, item => item is Quest));
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Other.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
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
        var quest = DefeatDeserters();
        Server.Call(() =>
        {
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

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 25)]
    [InlineData(2, 10, 100)]
    [InlineData(3, -10, -50)]
    public void NativeApplyQuestResult_ReplicatesTownRewardsToBothOriginalClients(
        int choice, int securityChange, int prosperityChange)
    {
        var ownerQuest = Accept();
        var serverQuest = (Quest)scenario.QuestOf(Server);
        Town serverTown = null!;
        uint handle = 0;
        Server.Call(() =>
        {
            serverTown = Server.GetRegisteredObject<Town>(townComponentId);
            serverTown.Security = 40;
            serverTown.Prosperity = 200;
            handle = Server.GetHandle(serverTown);
        });
        var originals = new Dictionary<EnvironmentInstance, Town>();
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                var town = client.GetRegisteredObject<Town>(townComponentId);
                Assert.Equal(handle, client.GetHandle(town));
                Assert.Equal(40, town.Security);
                Assert.Equal(200, town.Prosperity);
                originals.Add(client, town);
            });
            client.NetworkSentMessages.Clear();
        }
        Assert.Equal(2, originals.Count);
        Server.NetworkSentMessages.Clear();

        Owner.Call(() =>
        {
            using var replay = new AllowedThread();
            ownerQuest.ApplyQuestResult(ResultOf(ownerQuest));
            Assert.Equal(40, originals[Owner].Security);
            Assert.Equal(200, originals[Owner].Prosperity);
        });
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<GenericNetworkEvent<Town, byte[]>>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<GenericNetworkEvent<Town, byte[]>>());

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            using var ownerScope = new MainHeroSubstitutionScope(
                Server.GetRegisteredObject<Hero>(owner.HeroId),
                Server.GetRegisteredObject<MobileParty>(owner.MobilePartyId));
            Assert.False(CallOriginalPolicy.IsOriginalAllowed());
            serverQuest.ApplyQuestResult(ResultOf(serverQuest));
            Assert.Same(serverTown, Server.GetRegisteredObject<Town>(townComponentId));
            Assert.Equal(handle, Server.GetHandle(serverTown));
            Assert.Equal(40 + securityChange, serverTown.Security);
            Assert.Equal(200 + prosperityChange, serverTown.Prosperity);
        });

        var messages = Server.NetworkSentMessages.GetMessages<GenericNetworkEvent<Town, byte[]>>()
            .Where(message => message.InstanceId == handle).ToArray();
        output.WriteLine($"native reward choice={choice} town={townComponentId} handle={handle} server security={serverTown.Security} prosperity={serverTown.Prosperity}");
        foreach (var message in messages)
            output.WriteLine($"generated town message={message.GetType().Name} value={message.GetType().GetProperty("Value")!.GetValue(message)}");
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                var town = client.GetRegisteredObject<Town>(townComponentId);
                output.WriteLine($"client town={townComponentId} handle={client.GetHandle(town)} security={town.Security} prosperity={town.Prosperity}");
                Assert.Same(originals[client], town);
                Assert.Equal(handle, client.GetHandle(town));
                Assert.Equal(40 + securityChange, town.Security);
                Assert.Equal(200 + prosperityChange, town.Prosperity);
            });
            Assert.Empty(client.NetworkSentMessages.GetMessages<GenericNetworkEvent<Town, byte[]>>());
        }
        var security = Assert.Single(messages.Where(message => message.GetType().Name == "Town_Security_SetNetworkMessage"));
        Assert.Equal((float)(40 + securityChange), RawSerializer.Deserialize<float>(Assert.IsType<byte[]>(security.GetType().GetProperty("Value")!.GetValue(security))));
        var prosperityMessages = messages.Where(message => message.GetType().Name == "Town__prosperity_SetNetworkMessage").ToArray();
        if (prosperityChange == 0) Assert.Empty(prosperityMessages);
        else
        {
            var prosperity = Assert.Single(prosperityMessages);
            Assert.Equal((float)(200 + prosperityChange), RawSerializer.Deserialize<float>(Assert.IsType<byte[]>(prosperity.GetType().GetProperty("Value")!.GetValue(prosperity))));
        }
        Assert.Equal(prosperityChange == 0 ? 1 : 2, messages.Length);

        Quest.ExtortionByDesertersQuestResult ResultOf(Quest quest) => choice switch
        {
            0 => quest._questResultSuccess1,
            1 => quest._questResultSuccess2,
            2 => quest._questResultSuccess3,
            _ => quest._questResultFail1,
        };
    }

    private static void ChooseReward(Quest quest, int choice)
    {
        var consequences = quest.QuestCompletionDialogFlow.Lines
            .Where(line => line.ConsequenceDelegate != null).ToArray();
        Assert.Equal(3, consequences.Length);
        consequences[choice].ConsequenceDelegate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Notification_IsShownOnlyToOwnerWithoutAddingAJournalEntry(bool emptyLog)
    {
        var quest = Accept();
        var entries = quest.JournalEntries.Count;
        using var notifications = new MethodCallRecorder(AccessTools.GetDeclaredMethods(typeof(MBInformationManager))
            .Where(method => method.Name == nameof(MBInformationManager.AddQuickInformation)).ToArray());
        var state = new NetworkExtortionByDesertersState(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, "", "", (int)State.DesertersRunningAwayFromPlayer,
            emptyLog ? TextObject.GetEmpty() : null, false,
            (byte)ExtortionByDesertersNotice.DesertersNoticedPlayer);
        var wire = Server.SerializeForWire(state);
        Owner.SimulateMessage(this, wire);
        Other.SimulateMessage(this, wire);
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
    public void CoopVictory_FinalizesProgressAndCleansQuestPartiesWithoutAnEncounterMenuRequest()
    {
        DefeatDeserters();
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        var notice = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkExtortionByDesertersState>(),
            state => state.Notice == (byte)ExtortionByDesertersNotice.DesertersDefeated);
        Assert.Equal(QuestScenario.ControllerA, notice.ControllerId);
        Assert.Single(Owner.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(),
            state => state.Notice == (byte)ExtortionByDesertersNotice.DesertersDefeated);
        Assert.DoesNotContain(Other.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(),
            state => state.Notice == (byte)ExtortionByDesertersNotice.DesertersDefeated);
        Owner.Call(() =>
        {
            var ownerQuest = (Quest)scenario.QuestOf(Owner);
            Assert.Null(ownerQuest._deserterMobileParty);
            Assert.Null(ownerQuest._defenderMobileParty);
            Assert.Equal(State.DesertersAreDefeated, ownerQuest._currentState);
            Assert.Single(ownerQuest.JournalEntries,
                entry => entry.LogText?.Value == ownerQuest.OnDeserterPartyDefeatedLogText.Value);
        }, TextLinks);
    }

    private Quest DefeatDeserters()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OtherPartyId, out var otherParty));
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            // Healthy troops leave both quest parties for cleanup after the real map-event finalization.
            foreach (var party in new[] { deserters, defenders, ownerParty })
            {
                party.MemberRoster.AddToCounts(GameObjectCreator.CreateInitializedObject<CharacterObject>(), 1);
                Assert.True(party.IsActive);
                Assert.True(party.Party.NumberOfHealthyMembers > 0);
            }
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            deserters.SetPartyUsedByQuest(true);
            var battle = CreateBattle(deserters, defenders.Party, false);
            ownerParty.MapEventSide = battle.DefenderSide;
            Assert.Equal(State.DesertersMovingToSettlement, quest._currentState);
            Assert.False(quest._deserterBattleFinalizedForTheFirstTime);
            var entries = quest.JournalEntries.Count;
            battle.SetOverrideWinner(BattleSideEnum.Defender);
            battle.FinalizeEventAux();
            Assert.True(battle.IsFinalized);
            Assert.All(battle._sides, side => Assert.Empty(side.Parties));
            Assert.Equal(State.DesertersAreDefeated, quest._currentState);
            Assert.Equal(entries + 1, quest.JournalEntries.Count);
            Assert.Null(quest._deserterMobileParty);
            Assert.Null(quest._defenderMobileParty);
            Assert.False(quest._deserterBattleFinalizedForTheFirstTime);
            Assert.True(quest.IsOngoing);
            battle.FinalizeEventAux();
            Assert.Equal(entries + 1, quest.JournalEntries.Count);
            foreach (var party in new[] { deserters, defenders })
            {
                Assert.False(party.IsActive);
                Assert.Null(party.MapEvent);
            }
            Assert.True(ownerParty.IsActive);
            Assert.True(otherParty.IsActive);
            Assert.DoesNotContain(Server.InternalMessages.GetMessages<DestroyPartyApplied>(),
                message => message.DefeatedParty == ownerParty || message.DefeatedParty == otherParty);
        }, TextLinks.Concat(new[]
        {
            AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
            AccessTools.Method(typeof(MapEvent), nameof(MapEvent.MovePartyToSuitablePositionOnMapEventFinalize)),
        }));
        return quest;
    }

    [Fact]
    public void OwnerAttackingVillagePartyAfterVictory_StillFailsTheQuestAndAppliesThePenalty()
    {
        var quest = DefeatDeserters();
        var ownerQuest = (Quest)scenario.QuestOf(Owner);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            var villageParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            villageParty.SetCustomHomeSettlement(quest.QuestSettlement);
            Assert.Same(quest.QuestSettlement, villageParty.HomeSettlement);
            var security = quest.QuestSettlement.Village.Bound.Town.Security;
            // Initialize dispatches OnMapEventStarted through the real server relay.
            CreateBattle(ownerParty, villageParty.Party, false);
            Assert.False(quest.IsOngoing);
            Assert.Equal(-5, quest.RelationshipChangeWithQuestGiver);
            Assert.Equal(security - 10, quest.QuestSettlement.Village.Bound.Town.Security);
        }, TextLinks.Append(AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize))));
        Assert.Equal(IssueOutcome.QuestFail,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerLeavingRaidedVillage_FailsOnlyWhileTheQuestsDesertersAreRaiding(bool desertersRemain)
    {
        Quest quest;
        if (desertersRemain)
        {
            Accept();
            quest = (Quest)scenario.QuestOf(Server);
        }
        else quest = DefeatDeserters();

        // The recorder skips the original, so it must run before the guard that ignores a defeated quest's tick.
        using var ticks = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(Quest), nameof(Quest.TickDesertersPartyLogic)));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            var raiders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            if (desertersRemain) quest._deserterMobileParty = raiders;
            CreateBattle(raiders, quest.QuestSettlement.Party, true);
            CampaignEventDispatcher.Instance.OnSettlementLeft(ownerParty, quest.QuestSettlement);
            Assert.Equal(!desertersRemain, quest.IsOngoing);
            if (desertersRemain) Assert.Equal(-10, quest.RelationshipChangeWithQuestGiver);
            else Assert.Equal(State.DesertersAreDefeated, quest._currentState);
        }, TextLinks.Append(AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize))));
        var endings = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();
        if (desertersRemain) Assert.Equal(IssueOutcome.QuestFail, Assert.Single(endings).Outcome);
        else Assert.Empty(endings);
        Assert.Equal(desertersRemain ? 0 : 1, ticks.CountFor(Server));
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
    public void Timeout_WithValidCharacterProperties_AppliesThePenaltyAndOneJournalEntry()
    {
        using var binding = new TimeoutCharacterProperties();
        var quest = Accept(TextLinks.Skip(1));
        var mirror = (Quest)scenario.QuestOf(Server);
        string expected = null!;
        Server.Call(() => expected = AssertTimeoutTextRoundTrip(mirror));
        Owner.Call(() => quest.CompleteQuestWithTimeOut(), TextLinks.Skip(1));
        Assert.Equal(-10, mirror.RelationshipChangeWithQuestGiver);
        Assert.True(quest.IsFinalized);
        AssertTimeoutDelivery();
        Assert.Equal(expected, Assert.Single(quest.JournalEntries).LogText.ToString());
    }

    [Fact]
    public void Timeout_AppliesThePenaltyAndJournalOnTheOwningQuest()
    {
        var quest = Accept();
        var mirror = (Quest)scenario.QuestOf(Server);
        Server.Call(() =>
        {
            // Retain the original failing fixture: GetCharacterProperties is suppressed and returns null.
            var text = mirror.OnQuestTimedOutLogText;
            Assert.Contains("{=YjxCkglX}", text.Value);
            Assert.True(text.Attributes.ContainsKey("QUEST_GIVER"));
            Assert.Null(text.Attributes["QUEST_GIVER"]);
            output.WriteLine($"Original null-binding timeout: {text}");
        }, TextLinks);
        var statesBeforeTimeout = Server.NetworkSentMessages.GetMessages<NetworkExtortionByDesertersState>().Count();
        Owner.Call(() => quest.CompleteQuestWithTimeOut(), TextLinks);
        Assert.Equal(-10, mirror.RelationshipChangeWithQuestGiver);
        Assert.True(quest.IsFinalized);
        var entry = Assert.Single(quest.JournalEntries).LogText;
        AssertTimeoutDelivery();
        var state = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkExtortionByDesertersState>()
            .Skip(statesBeforeTimeout));
        var stateWire = Owner.DeserializeFromWire<NetworkExtortionByDesertersState>(Server.SerializeForWire(state));
        var snapshot = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        var snapshotWire = Owner.DeserializeFromWire<NetworkIssueJournal>(Server.SerializeForWire(snapshot));
        Assert.Equal(stateWire.Log.Value, entry.Value);
        Assert.Equal(Assert.Single(snapshotWire.Logs).ToString(), entry.ToString());
        Assert.DoesNotContain("ERROR:", entry.ToString());
        output.WriteLine($"Owner single timeout after both serialized paths: {entry}");
    }

    private void AssertTimeoutDelivery()
    {
        var request = Assert.Single(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Equal(IssueOutcome.QuestTimeOut, request.Outcome);
        Assert.Equal(scenario.NotableId, request.IssueOwnerId);
        Assert.Equal(QuestScenario.IssueId, request.IssueId);
        Assert.Equal(IssueOutcome.QuestTimeOut,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        var snapshot = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        Assert.Equal(scenario.NotableId, snapshot.IssueOwnerId);
        Assert.Equal(QuestScenario.IssueId, snapshot.IssueId);
        Assert.True(snapshot.IsQuestJournal);
        Assert.Single(snapshot.Logs);
        Other.Call(() =>
        {
            Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, item => item is Quest);
            var notable = Other.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.NotNull(notable);
            var issue = notable.Issue;
            Assert.Null(issue?.IssueQuest);
            if (issue != null) Assert.Empty(issue.JournalEntries);
        });
    }

    private string AssertTimeoutTextRoundTrip(Quest mirror)
    {
        var text = mirror.OnQuestTimedOutLogText;
        var envelope = new NetworkIssueJournal(scenario.NotableId, QuestScenario.IssueId, true, new[] { text });
        var roundTrip = Owner.DeserializeFromWire<NetworkIssueJournal>(Server.SerializeForWire(envelope));
        var rendered = AssertValidTimeoutText(text);
        Assert.Equal(rendered, AssertValidTimeoutText(Assert.Single(roundTrip.Logs)));
        return rendered;
    }

    private static string AssertValidTimeoutText(TextObject text)
    {
        Assert.Contains("{=YjxCkglX}", text.Value);
        var properties = Assert.IsType<TextObject>(text.Attributes["QUEST_GIVER"]);
        var name = Assert.IsType<TextObject>(properties.Attributes["NAME"]);
        Assert.IsType<int>(properties.Attributes["GENDER"]);
        var link = Assert.IsType<TextObject>(properties.Attributes["LINK"]);
        Assert.False(TextObject.IsNullOrEmpty(name));
        Assert.False(TextObject.IsNullOrEmpty(link));
        var rendered = text.ToString();
        Assert.DoesNotContain("ERROR:", rendered);
        Assert.Contains(name.ToString(), rendered);
        Assert.Contains(link.ToString(), rendered);
        return rendered;
    }

    private sealed class TimeoutCharacterProperties : IDisposable
    {
        private readonly Harmony harmony = new($"timeout-character-properties-{Guid.NewGuid()}");
        private readonly MethodInfo method = AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties));

        public TimeoutCharacterProperties() => harmony.Patch(method,
            prefix: new HarmonyMethod(typeof(TimeoutCharacterProperties), nameof(Prefix)));

        public void Dispose() => harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);

        private static bool Prefix(CharacterObject character, ref TextObject __result)
        {
            var link = new TextObject("{=!}<a style=\"Link.Hero\" href=\"{ID}\"><b>{NAME}</b></a>");
            link.SetTextVariable("ID", character.HeroObject.StringId);
            link.SetTextVariable("NAME", character.Name);
            __result = TextObject.GetEmpty();
            __result.SetTextVariable("NAME", character.Name);
            __result.SetTextVariable("GENDER", character.IsFemale ? 1 : 0);
            __result.SetTextVariable("LINK", link);
            return false;
        }
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

    [Theory]
    [InlineData(99f)]
    [InlineData(213.6168f)]
    public void NativeGiverDeath_CancelsBeforeNotableReplacementAndReleasesOnlyTheOwnersQuest(float power)
    {
        var ownerQuest = Accept();
        var quest = (Quest)scenario.QuestOf(Server);
        var secondId = AddSecondIssue();
        Quest otherQuest = null!;
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(secondId, out var secondGiver));
            secondGiver.Issue.StartIssueWithQuest();
            otherQuest = (Quest)secondGiver.Issue.IssueQuest;
            otherQuest.StartQuest();
        }, TextLinks);
        var replacementId = environment.CreateRegisteredObject<Hero>();
        var log = string.Empty;
        Server.Call(() =>
        {
            var giver = quest.QuestGiver;
            var issue = giver.Issue;
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.OtherHeroId, out var other));
            var mainHero = Hero.MainHero;
            var mainParty = MobileParty.MainParty;
            var now = Campaign.Current.MapTimeTracker.Now;
            var ownerBefore = (owner.Gold, owner.Clan.Renown, giver.GetRelation(owner));
            var otherBefore = (other.Gold, other.Clan.Renown, giver.GetRelation(other));
            var traitXp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            traitXp[scenario.OwnerHeroId] = new Dictionary<string, int> { ["Honor"] = 37 };
            traitXp[scenario.OtherHeroId] = new Dictionary<string, int> { ["Honor"] = 53 };
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(secondId, out var secondGiver));
            var secondQuest = (Quest)secondGiver.Issue.IssueQuest;
            var otherDeserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            secondQuest._deserterMobileParty = otherDeserters;
            otherDeserters.SetPartyUsedByQuest(true);
            otherDeserters.Ai.SetDoNotMakeNewDecisions(true);
            var deserters = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenders = GameObjectCreator.CreateInitializedObject<MobileParty>();
            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            deserters.SetPartyUsedByQuest(true);
            deserters.Aggressiveness = 0f;
            deserters.Ai.SetDoNotMakeNewDecisions(true);
            giver._power = power;
            using (new AllowedThread()) giver.Clan = null;
            var ownership = Server.Resolve<IIssueOwnershipRegistry>();
            Assert.True(Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(issue, out var resolved));
            Assert.Same(owner, resolved);

            using var native = new NativeNotableDeathScope(replacement);
            KillCharacterAction.ApplyInternal(giver, null, KillCharacterAction.KillCharacterActionDetail.Murdered,
                showNotification: true, isForced: true);

            var transferred = Campaign.Current.IssueManager.Issues.TryGetValue(replacement, out var successorIssue);
            var ownerResolves = Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(issue, out _);
            var oldKeyRemains = ownership.TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _);
            output.WriteLine($"power={power} dead={giver.IsDead} deadIssueNull={giver.Issue == null} " +
                $"transferred={transferred && ReferenceEquals(issue, successorIssue)} ongoing={quest.IsOngoing} " +
                $"ownerResolves={ownerResolves} oldKeyRemains={oldKeyRemains} " +
                $"questUsed={deserters.IsCurrentlyUsedByAQuest} noDecisions={deserters.Ai.DoNotMakeNewDecisions}");
            Assert.Equal(power >= 100f ? 1 : 0, native.Replacements);
            Assert.True(giver.IsDead);
            Assert.Null(giver.Issue);
            Assert.False(quest.IsOngoing);
            Assert.True(quest.IsFinalized);
            Assert.False(transferred);
            Assert.False(oldKeyRemains);
            Assert.False(ownerResolves);
            Assert.DoesNotContain(issue, Campaign.Current.IssueManager.Issues.Values);
            Assert.True(deserters.IsActive);
            Assert.False(deserters.IsCurrentlyUsedByAQuest);
            Assert.False(deserters.Ai.DoNotMakeNewDecisions);
            Assert.Equal(1f, deserters.Aggressiveness);
            Assert.False(defenders.IsActive);
            Assert.Null(quest._defenderMobileParty);
            Assert.Same(mainHero, Hero.MainHero);
            Assert.Same(mainParty, MobileParty.MainParty);
            Assert.Equal(now, Campaign.Current.MapTimeTracker.Now);
            Assert.Equal(ownerBefore, (owner.Gold, owner.Clan.Renown, giver.GetRelation(owner)));
            Assert.Equal(otherBefore, (other.Gold, other.Clan.Renown, giver.GetRelation(other)));
            Assert.Equal(37, traitXp[scenario.OwnerHeroId]["Honor"]);
            Assert.Equal(53, traitXp[scenario.OtherHeroId]["Honor"]);
            Assert.True(secondQuest.IsOngoing);
            Assert.True(otherDeserters.IsCurrentlyUsedByAQuest);
            Assert.True(otherDeserters.Ai.DoNotMakeNewDecisions);
            Assert.True(ownership.TryGetOwner(secondId, "second_deserters", out var secondOwner));
            Assert.Equal(QuestScenario.ControllerB, secondOwner);
            log = Assert.Single(quest.JournalEntries).LogText.Value;
            Assert.Contains("died", log);
        }, TextLinks);
        Assert.Equal(IssueOutcome.QuestCancel,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Single(ownerQuest.JournalEntries, entry => entry.LogText?.Value == log);
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
        });
        Other.Call(() =>
        {
            Assert.Same(otherQuest, Assert.Single(Campaign.Current.QuestManager.Quests));
            Assert.True(otherQuest.IsOngoing);
            Assert.DoesNotContain(otherQuest.JournalEntries, entry => entry.LogText?.Value == log);
        });
        Assert.Single(Owner.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(), state => state.Log?.Value == log);
        Assert.DoesNotContain(Other.InternalMessages.GetMessages<NetworkExtortionByDesertersState>(), state => state.Log?.Value == log);
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(Other.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        foreach (var client in new[] { Owner, Other })
            client.Call(() => Assert.False(client.Resolve<IIssueOwnershipRegistry>()
                .TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeGiverDeath_StillTransfersUnacceptedOffersIncludingOtherIssueTypes(bool otherIssueType)
    {
        var replacementId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
            using (new AllowedThread())
            {
                giver.Clan = null;
                if (otherIssueType)
                {
                    var otherIssue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(giver, null);
                    Campaign.Current.IssueManager._issues[giver] = otherIssue;
                    giver.OnIssueCreatedForHero(otherIssue);
                }
            }
            giver._power = 213.6168f;
            var issue = giver.Issue;
            using var native = new NativeNotableDeathScope(replacement);
            KillCharacterAction.ApplyInternal(giver, null, KillCharacterAction.KillCharacterActionDetail.Murdered,
                showNotification: true, isForced: true);
            Assert.Equal(1, native.Replacements);
            Assert.True(giver.IsDead);
            Assert.Null(giver.Issue);
            Assert.Same(issue, replacement.Issue);
            Assert.Same(replacement, issue.IssueOwner);
            Assert.Same(issue, Campaign.Current.IssueManager.Issues[replacement]);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(giver));
        }, TextLinks);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    private sealed class NativeNotableDeathScope : IDisposable
    {
        private static NativeNotableDeathScope? current;
        private readonly Harmony harmony = new($"deserter-native-death-{Guid.NewGuid()}");
        private readonly NotablesCampaignBehavior notables = new();
        private readonly Hero replacement;
        public int Replacements { get; private set; }

        public NativeNotableDeathScope(Hero replacement)
        {
            this.replacement = replacement;
            current = this;
            Assert.Equal(100, Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit);
            // Supply the relative at the native appearance-creation boundary; the death listener and issue transfer run normally.
            harmony.Patch(AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.CreateRelativeNotableHero)),
                prefix: new HarmonyMethod(typeof(NativeNotableDeathScope), nameof(CreateRelativePrefix)));
            notables.RegisterEvents();
        }

        private static bool CreateRelativePrefix(ref Hero __result)
        {
            current!.Replacements++;
            __result = current.replacement;
            return false;
        }

        public void Dispose()
        {
            CampaignEvents.Instance.RemoveListeners(notables);
            harmony.Unpatch(AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.CreateRelativeNotableHero)),
                HarmonyPatchType.Prefix, harmony.Id);
            current = null;
        }
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
