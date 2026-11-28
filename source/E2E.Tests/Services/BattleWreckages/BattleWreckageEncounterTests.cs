using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.BattleWreckages;
using GameInterface.Services.BattleWreckages.Data;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

public class BattleWreckageEncounterTests : BattleWreckageTestBase
{
    public BattleWreckageEncounterTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerLeave_InvestigationCompleted_SyncsInvestigatedToAllClients()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            // Epic wreckages stay on the map once investigated, so clients still have one to check
            SetWreckageType(battleWreckage, BattleWreckage.WreckageType.Epic);

            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();
            encounterTracker.AddEncounter(new BattleWreckageEncounter(hero, battleWreckage));

            Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().LeaveWreckageEncounter(
                hero,
                BattleWreckageCampaignBehavior.WreckageLeaveReason.InvestigationCompleted);

            Assert.False(encounterTracker.TryGetEncounter(hero, out _));
        });

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.True(battleWreckage.IsInvestigated);
            });
        }
    }

    [Fact]
    public void ServerSelect_RecoverTroops_DecidesThenRollsTroopsAndRestoresBehavior()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            var (hero, encounter, behavior, woundedTroop) = CreateRecoverTroopsEncounter(battleWreckageId);
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();

            var mainParty = Campaign.Current.MainParty;
            var behaviorLootedTroops = behavior._lootedTroops;
            var behaviorLootedItems = behavior._lootedItems;
            var behaviorConsequenceExplanations = behavior._consequenceExplanations;

            // Act, the first roll asks the player to decide on recovering troops
            campaignBehaviorInterface.SelectWreckageConsequences(hero);

            // Assert
            var decision = Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
            // A small wreckage rolls RandomInt(2, 5), whose max is exclusive
            Assert.InRange(decision.RecoverTroopCount, 2, 4);
            Assert.Equal(BattleWreckageCampaignBehavior.RecoverTroopsConsequenceId, encounter.OverriddenConsequenceId);
            Assert.Empty(Server.InternalMessages.GetMessages<WreckageInvestigationResultsApplied>());

            // The hero's medicine skill decides how many of the decided troops come back, and how long the rescue takes
            int expectedTroopCount;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                expectedTroopCount = behavior.GetEffectiveTroopCountToRecover(decision.RecoverTroopCount);
            }
            Assert.Equal(CampaignTime.Hours(Math.Max(2, expectedTroopCount)), decision.RequiredInvestigationDuration);
            Assert.Equal(decision.RequiredInvestigationDuration, encounter.RequiredInvestigationDuration);

            // Act, the accepted recovery rolls the troops
            campaignBehaviorInterface.AcceptRecoverTroops(hero);
            campaignBehaviorInterface.SelectWreckageConsequences(hero);

            // Assert, the results go out with the roll
            Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
            var results = Assert.Single(Server.InternalMessages.GetMessages<WreckageInvestigationResultsApplied>());
            Assert.Equal(expectedTroopCount, results.LootedTroops.TotalManCount);
            Assert.All(results.LootedTroops.GetTroopRoster(), element => Assert.Same(woundedTroop, element.Character));
            Assert.Single(results.ConsequenceExplanations);

            Assert.Same(mainParty, Campaign.Current.MainParty);
            Assert.Same(behaviorLootedTroops, behavior._lootedTroops);
            Assert.Same(behaviorLootedItems, behavior._lootedItems);
            Assert.Same(behaviorConsequenceExplanations, behavior._consequenceExplanations);
            Assert.Null(behavior._encounteredBattleWreckage);
            Assert.True(string.IsNullOrEmpty(behavior._currentOverriddenConsequenceId));
            Assert.False(behavior._isOverriddenConsequenceAccepted);
            Assert.Equal(0, behavior._recoverTroopCountsToAdd);
            Assert.Equal(CampaignTime.Zero, behavior._requiredInvestigationDuration);
            Assert.Equal(0, behavior._lootedGoldAmount);
            Assert.Empty(behavior._selectedConsequences);
        });
    }

    [Fact]
    public void ServerSelect_DeclinedRecoverTroops_RollsNothingFoundWithoutAskingAgain()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            var (hero, encounter, _, _) = CreateRecoverTroopsEncounter(battleWreckageId);
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();

            // Act, declining sends another select without accepting first, and a double-click sends it twice
            campaignBehaviorInterface.SelectWreckageConsequences(hero);
            campaignBehaviorInterface.SelectWreckageConsequences(hero);
            campaignBehaviorInterface.SelectWreckageConsequences(hero);

            // Assert, only the first decline rolls and sends results
            Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
            var results = Assert.Single(Server.InternalMessages.GetMessages<WreckageInvestigationResultsApplied>());
            Assert.Equal(0, results.LootedTroops.TotalManCount);
            Assert.Single(results.ConsequenceExplanations);
            Assert.False(encounter.IsInvestigating);
        });
    }

    [Fact]
    public void ServerSelect_LoadsDecisionThenResultsOnTheClient()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();
        var owner = Clients.First();
        string? woundedTroopId = null;
        Hero? hero = null;

        Server.Call(() =>
        {
            var (encounterHero, _, _, woundedTroop) = CreateRecoverTroopsEncounter(battleWreckageId);
            hero = encounterHero;
            Assert.True(Server.ObjectManager.TryGetId(woundedTroop, out woundedTroopId));
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("wreckage_looter", heroId, null, null, null)));
            playerManager.SetPeer("wreckage_looter", owner.NetPeer);
        });

        BattleWreckageCampaignBehavior? clientBehavior = null;
        owner.Call(() => clientBehavior = AddBattleWreckageBehavior());
        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));

        // Act, the first roll asks the player to decide on recovering troops
        Server.Call(() => Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().SelectWreckageConsequences(hero!));
        owner.PumpGameThread();

        // Assert, the client's wreckage menu reads the override to resume an interrupted rescue
        var decision = Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
        Assert.Equal(new[] { BattleWreckageCampaignBehavior.MenuIdRecoverTroopsDecision }, menus.MenusFor(owner));
        owner.Call(() =>
        {
            Assert.Equal(BattleWreckageCampaignBehavior.RecoverTroopsConsequenceId, clientBehavior!._currentOverriddenConsequenceId);
            Assert.Equal(decision.RecoverTroopCount, clientBehavior._recoverTroopCountsToAdd);
            Assert.Equal(decision.RequiredInvestigationDuration, clientBehavior._requiredInvestigationDuration);
        });
        menus.Clear();

        // Act, the accepted recovery rolls the troops
        Server.Call(() =>
        {
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
            campaignBehaviorInterface.AcceptRecoverTroops(hero!);
            campaignBehaviorInterface.SelectWreckageConsequences(hero!);
        });
        owner.PumpGameThread();

        // Assert, one message carries the results, so the client has them when it opens the connection menu
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkShowWreckageInvestigationResults>());
        var results = Assert.Single(Server.InternalMessages.GetMessages<WreckageInvestigationResultsApplied>());
        Assert.Equal(new[] { BattleWreckageCampaignBehavior.BattleWreckageResultsConnectionMenuId }, menus.MenusFor(owner));
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<CharacterObject>(woundedTroopId!, out var woundedTroop));
            Assert.Equal(results.LootedTroops.TotalManCount, clientBehavior!._lootedTroops.TotalManCount);
            Assert.All(clientBehavior._lootedTroops.GetTroopRoster(), element => Assert.Same(woundedTroop, element.Character));
            Assert.Single(clientBehavior._consequenceExplanations);
        });
    }

    [Fact]
    public void ServerSelect_RevealedHideout_PinsItOnTheFindersMap()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var owner = Clients.First();
        Hero? hero = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            hideout.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
            hideout.IsVisible = false;
            Campaign.Current._hideouts.Add(hideout.Hideout);

            // What vanilla GainGoldConsequence does on its 50% hideout roll, so the roll doesn't depend on chance
            var behavior = AddBattleWreckageBehavior();
            behavior._wreckageConsequences.RemoveAll(consequence =>
                consequence.StringId != BattleWreckageCampaignBehavior.NothingFoundConsequenceId);
            behavior._wreckageConsequences.Add(new BattleWreckageCampaignBehavior.BattleWreckageConsequence(
                "RevealHideout",
                condition: null,
                () =>
                {
                    hideout.IsVisible = true;
                    Campaign.Current.VisualTrackerManager.RegisterObject(hideout);
                },
                chance: 1f));

            hero = CreatePartyLeader();
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("hideout_finder", heroId, null, null, null)));
            playerManager.SetPeer("hideout_finder", owner.NetPeer);

            var encounter = new BattleWreckageEncounter(hero, battleWreckage) { IsInvestigating = true };
            Server.Resolve<IBattleWreckageEncounterTracker>().AddEncounter(encounter);
        });

        owner.Call(() => AddBattleWreckageBehavior());
        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));

        // Act
        Server.Call(() => Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().SelectWreckageConsequences(hero!));
        owner.PumpGameThread();

        // Assert, the server's pin isn't the finder's, so the hideout goes with the results and the client pins it
        var results = Assert.Single(Server.InternalMessages.GetMessages<WreckageInvestigationResultsApplied>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            Assert.Same(hideout, results.FoundHideout);
        });
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            Assert.True(Campaign.Current.VisualTrackerManager.CheckTracked(hideout));
        });
    }

    [Fact]
    public void ServerStart_NewWreckage_TracksEncounterWithVanillaDuration()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            var behavior = AddBattleWreckageBehavior();
            var hero = CreatePartyLeader();
            var mainParty = Campaign.Current.MainParty;

            // Act
            Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().StartWreckageEncounter(battleWreckage, hero);

            // Assert
            Assert.True(Server.Resolve<IBattleWreckageEncounterTracker>().TryGetEncounter(hero, out var encounter));
            Assert.Same(battleWreckage, encounter.BattleWreckage);
            Assert.True(encounter.IsInvestigating);

            // A small wreckage's base duration is 10 to 20 hours, and roguery takes off up to half of it
            Assert.InRange(encounter.RequiredInvestigationDuration.ToHours, 5, 20);

            var started = Assert.Single(Server.InternalMessages.GetMessages<WreckageEncounterStarted>());
            Assert.False(started.IsInterrupted);
            Assert.Equal(encounter.RequiredInvestigationDuration, started.RequiredInvestigationDuration);

            // The substitution scope and the borrowed encountered wreckage are both put back
            Assert.Same(mainParty, Campaign.Current.MainParty);
            Assert.Null(behavior._encounteredBattleWreckage);
        });
    }

    [Fact]
    public void ServerStart_InterruptedWreckage_ResumesWithRemainingDuration()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            AddBattleWreckageBehavior();
            var hero = CreatePartyLeader();
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();

            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, hero);
            campaignBehaviorInterface.LeaveWreckageEncounter(hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);

            Assert.True(Server.Resolve<IBattleWreckageEncounterTracker>().TryGetEncounter(hero, out var interruptedEncounter));

            // Test time doesn't move, so the interrupt leaves the duration as rolled. A small wreckage never rolls
            // 3 hours, so this only survives the resume if the remaining time is kept rather than rolled again
            var remainingDuration = CampaignTime.Hours(3);
            interruptedEncounter.RequiredInvestigationDuration = remainingDuration;

            // Act
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, hero);

            // Assert
            Assert.True(Server.Resolve<IBattleWreckageEncounterTracker>().TryGetEncounter(hero, out var encounter));
            Assert.Same(interruptedEncounter, encounter);
            Assert.True(encounter.IsInvestigating);
            Assert.Equal(remainingDuration, encounter.RequiredInvestigationDuration);

            var started = Server.InternalMessages.GetMessages<WreckageEncounterStarted>().ToList();
            Assert.Equal(2, started.Count);
            Assert.True(started[1].IsInterrupted);
            Assert.Equal(remainingDuration, started[1].RequiredInvestigationDuration);
        });
    }

    // An uninvestigated Epic wreckage is locked like any other, only its investigated remains are open to everyone
    [Theory]
    [InlineData(BattleWreckage.WreckageType.Small)]
    [InlineData(BattleWreckage.WreckageType.Epic)]
    public void ServerStart_WreckageHeldByInterruptedPlayer_DeniesSecondPlayer(BattleWreckage.WreckageType wreckageType)
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            SetWreckageType(battleWreckage, wreckageType);

            AddBattleWreckageBehavior();
            var firstHero = CreatePartyLeader();
            var secondHero = CreatePartyLeader();
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

            // The interrupted player keeps the wreckage so they can come back and resume
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, firstHero);
            campaignBehaviorInterface.LeaveWreckageEncounter(firstHero, BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);

            // Act
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, secondHero);

            // Assert
            var denied = Assert.Single(Server.InternalMessages.GetMessages<WreckageEncounterDenied>());
            Assert.Same(secondHero, denied.MainHero);
            Assert.False(encounterTracker.TryGetEncounter(secondHero, out _));

            Assert.True(encounterTracker.TryGetEncounter(firstHero, out var firstEncounter));
            Assert.Same(battleWreckage, firstEncounter.BattleWreckage);
            Assert.Single(Server.InternalMessages.GetMessages<WreckageEncounterStarted>());
        });
    }

    [Fact]
    public void ServerPartyEncounter_AttackedInvestigator_InterruptsTheirInvestigation()
    {
        // Arrange, lord parties have a leader hero, which is what the encounter looks up
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            AddBattleWreckageBehavior();
            var investigatorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var attackerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = investigatorParty.LeaderHero;
            Assert.NotNull(hero);
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, hero);

            // Act, a party nobody tracks is attacked first, then the investigator
            campaignBehaviorInterface.OnPartyEncounter(investigatorParty.Party, attackerParty.Party);
            campaignBehaviorInterface.OnPartyEncounter(attackerParty.Party, investigatorParty.Party);

            // Assert
            Assert.True(encounterTracker.TryGetEncounter(hero, out var encounter));
            Assert.False(encounter.IsInvestigating);
            var left = Assert.Single(Server.InternalMessages.GetMessages<WreckageEncounterLeft>());
            Assert.Same(hero, left.MainHero);
            Assert.Equal(BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted, left.Reason);
        });
    }

    [Fact]
    public void ClientStart_InterruptedThenResumed_LoadsTheServersTimeLeft()
    {
        // Arrange, the first client's player investigates through the real request and reply messages
        var battleWreckageId = CreateServerWreckage();
        var owner = Clients.First();
        string? heroId = null;
        string? partyId = null;

        Server.Call(() =>
        {
            AddBattleWreckageBehavior();
            var hero = CreatePartyLeader();
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.PartyBelongedTo, out partyId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("wreckage_investigator", heroId, partyId, null, null)));
            playerManager.SetPeer("wreckage_investigator", owner.NetPeer);
        });

        BattleWreckageCampaignBehavior? clientBehavior = null;
        owner.Call(() =>
        {
            clientBehavior = AddBattleWreckageBehavior();
            Assert.True(owner.ObjectManager.TryGetObject<MobileParty>(partyId!, out var party));
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(heroId!, out var hero));
            Campaign.Current.MainParty = party;
            Game.Current.PlayerTroop = hero.CharacterObject;
        });
        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)));

        // Act
        RequestStartWreckageEncounter(owner, battleWreckageId, heroId!);

        // Assert, the client uses the server's duration, not its own roll
        CampaignTime rolledDuration = default;
        Server.Call(() => rolledDuration = GetServerEncounter(heroId!).RequiredInvestigationDuration);
        owner.Call(() =>
        {
            Assert.False(clientBehavior!._isEncounteredWreckageInterrupted);
            Assert.Equal(rolledDuration, clientBehavior._requiredInvestigationDuration);
        });

        // Act, the server interrupts the investigation
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId!, out var hero));
            Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().LeaveWreckageEncounter(
                hero,
                BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);
        });
        owner.PumpGameThread();

        // Assert, the client keeps the wreckage and the time left, which is all of it while test time is pinned
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.Same(battleWreckage, clientBehavior!._encounteredBattleWreckage);
            Assert.Equal(rolledDuration, clientBehavior._requiredInvestigationDuration);
        });

        // Act, coming back resumes. A small wreckage never rolls 3 hours, so the client can only show it from the server
        var remainingDuration = CampaignTime.Hours(3);
        Server.Call(() => GetServerEncounter(heroId!).RequiredInvestigationDuration = remainingDuration);
        RequestStartWreckageEncounter(owner, battleWreckageId, heroId!);

        // Assert
        owner.Call(() =>
        {
            Assert.True(clientBehavior!._isEncounteredWreckageInterrupted);
            Assert.Equal(remainingDuration, clientBehavior._requiredInvestigationDuration);
        });
        Assert.Equal(new[] { BattleWreckageCampaignBehavior.MenuIdBattleWreckage, BattleWreckageCampaignBehavior.MenuIdBattleWreckage }, menus.MenusFor(owner));
    }

    [Fact]
    public void DisconnectedPlayer_KeepsTheirInvestigationToResumeOnReturn()
    {
        // Arrange
        const int elapsedHours = 2;
        var battleWreckageId = CreateServerWreckage();
        var otherBattleWreckageId = CreateServerWreckage();

        var clock = UseMapClock();
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(otherBattleWreckageId, out var otherBattleWreckage));
                StartMapClock();
                AddBattleWreckageBehavior();
                var hero = CreateDisconnectedPlayerLeader("wreckage_leaver");
                var connectedHero = CreatePartyLeader();
                var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
                var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

                campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, hero);
                campaignBehaviorInterface.StartWreckageEncounter(otherBattleWreckage, connectedHero);
                Assert.True(encounterTracker.TryGetEncounter(hero, out var encounter));
                Assert.True(encounterTracker.TryGetEncounter(connectedHero, out var connectedEncounter));
                var rolledDuration = encounter.RequiredInvestigationDuration;
                var connectedStartTime = connectedEncounter.InvestigationStartTime;
                AdvanceHours(elapsedHours);

                // Act
                campaignBehaviorInterface.InterruptDisconnectedPlayers();

                // Assert, the encounter is kept as interrupted with the time it has left, and the hourly tick expires it from here
                Assert.False(encounter.IsInvestigating);
                Assert.Equal(CampaignTime.Now, encounter.LastInteractionTime);
                var remainingDuration = encounter.RequiredInvestigationDuration;
                Assert.Equal(rolledDuration.ToHours - elapsedHours, remainingDuration.ToHours, 3);

                // A connected player's investigation keeps running
                Assert.True(connectedEncounter.IsInvestigating);
                Assert.Equal(connectedStartTime, connectedEncounter.InvestigationStartTime);
                Assert.Equal(CampaignTime.Zero, connectedEncounter.LastInteractionTime);

                // A second sweep has to skip it, otherwise the elapsed time counts from CampaignTime.Zero and
                // the 1 hour floor in the interrupt replaces the time left
                AdvanceHours(1);
                campaignBehaviorInterface.InterruptDisconnectedPlayers();
                Assert.Equal(remainingDuration, encounter.RequiredInvestigationDuration);

                // Coming back resumes it
                campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, hero);
                var started = Server.InternalMessages.GetMessages<WreckageEncounterStarted>().ToList();
                Assert.Equal(3, started.Count);
                Assert.Same(hero, started[2].MainHero);
                Assert.True(started[2].IsInterrupted);
                Assert.Equal(remainingDuration, started[2].RequiredInvestigationDuration);
            });
        }
        finally
        {
            clock.UnpatchAll(clock.Id);
        }
    }

    [Fact]
    public void ServerStart_WreckageHeldByDisconnectedPlayer_GivesItToSecondPlayer()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            AddBattleWreckageBehavior();
            var offlineHero = CreateDisconnectedPlayerLeader("wreckage_holder");
            var secondHero = CreatePartyLeader();
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, offlineHero);
            campaignBehaviorInterface.InterruptDisconnectedPlayers();
            Assert.True(encounterTracker.TryGetEncounter(offlineHero, out var offlineEncounter));
            Assert.False(offlineEncounter.IsInvestigating);

            // Act
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, secondHero);

            // Assert, the second player starts fresh and the offline player's investigation ends
            Assert.Empty(Server.InternalMessages.GetMessages<WreckageEncounterDenied>());
            Assert.False(encounterTracker.TryGetEncounter(offlineHero, out _));
            Assert.True(encounterTracker.TryGetEncounter(secondHero, out var encounter));
            Assert.Same(battleWreckage, encounter.BattleWreckage);
            var started = Server.InternalMessages.GetMessages<WreckageEncounterStarted>().ToList();
            Assert.Equal(2, started.Count);
            Assert.Same(secondHero, started[1].MainHero);
            Assert.False(started[1].IsInterrupted);
            var left = Server.InternalMessages.GetMessages<WreckageEncounterLeft>().ToList();
            Assert.Equal(BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired, left.Last().Reason);
            Assert.Same(offlineHero, left.Last().MainHero);
        });
    }

    [Fact]
    public void ServerStart_InvestigatedEpicRemains_LetsSeveralPlayersView()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            SetWreckageType(battleWreckage, BattleWreckage.WreckageType.Epic);
            battleWreckage.OnWreckageInvestigated();

            AddBattleWreckageBehavior();
            var firstHero = CreatePartyLeader();
            var secondHero = CreatePartyLeader();
            var campaignBehaviorInterface = Server.Resolve<IBattleWreckageCampaignBehaviorInterface>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();

            // Act
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, firstHero);
            campaignBehaviorInterface.StartWreckageEncounter(battleWreckage, secondHero);

            // Assert
            Assert.Empty(Server.InternalMessages.GetMessages<WreckageEncounterDenied>());
            Assert.Equal(2, Server.InternalMessages.GetMessages<WreckageEncounterStarted>().Count());
            Assert.True(encounterTracker.TryGetEncounter(firstHero, out _));
            Assert.True(encounterTracker.TryGetEncounter(secondHero, out _));
        });
    }

    // Recover troops is the only consequence that can roll, and it always does, so no roll depends on chance.
    // Run inside Server.Call
    private (Hero Hero, BattleWreckageEncounter Encounter, BattleWreckageCampaignBehavior Behavior, CharacterObject WoundedTroop)
        CreateRecoverTroopsEncounter(string battleWreckageId)
    {
        Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

        // Wounded low tier troops let vanilla RecoverTroopsCondition pass and are what gets recovered
        var woundedTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        battleWreckage.AttackerWoundedInBattle.AddToCounts(woundedTroop, 5, woundedCount: 5);

        var behavior = AddBattleWreckageBehavior();

        // NothingFound never rolls (chance -1) but stays as the fallback when nothing else is picked
        var recoverTroops = behavior.FindConsequenceWithId(BattleWreckageCampaignBehavior.RecoverTroopsConsequenceId);
        behavior._wreckageConsequences.RemoveAll(consequence =>
            consequence.StringId != BattleWreckageCampaignBehavior.NothingFoundConsequenceId);
        behavior._wreckageConsequences.Add(new BattleWreckageCampaignBehavior.BattleWreckageConsequence(
            recoverTroops.StringId, recoverTroops.Condition, recoverTroops.Consequence, chance: 1f, canOverride: true));

        var hero = CreatePartyLeader();

        // Running, as StartWreckageEncounter leaves it
        var encounter = new BattleWreckageEncounter(hero, battleWreckage) { IsInvestigating = true };
        Server.Resolve<IBattleWreckageEncounterTracker>().AddEncounter(encounter);

        return (hero, encounter, behavior, woundedTroop);
    }

    // Run inside Server.Call
    private BattleWreckageEncounter GetServerEncounter(string heroId)
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
        Assert.True(Server.Resolve<IBattleWreckageEncounterTracker>().TryGetEncounter(hero, out var encounter));
        return encounter;
    }

    // Sends the client's start request the way vanilla OnPartyInteraction does, then lets the server reply
    private void RequestStartWreckageEncounter(EnvironmentInstance owner, string battleWreckageId, string heroId)
    {
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            owner.Resolve<IMessageBroker>().Publish(this, new RequestStartWreckageEncounter(battleWreckage, hero));
        });
        Server.PumpGameThread();
        owner.PumpGameThread();
    }
}
