using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.BattleWreckages;
using GameInterface.Services.BattleWreckages.Data;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

public class BattleWreckageLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public BattleWreckageLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncAllClients()
    {
        // Act
        var battleWreckageId = CreateServerWreckage();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.Contains(battleWreckage, Campaign.Current.Wreckages);
                Assert.Equal(BattleWreckage.WreckageType.Small, battleWreckage.WreckageTypeCategory);
            });
        }
    }

    [Fact]
    public void ServerCreate_BattleWreckage_AnnouncesOnClientsAfterItsFieldsArrive()
    {
        // Arrange, the map visual reads the wreckage's type and position once, when the interactable is created
        var typesWhenAnnounced = new Dictionary<EnvironmentInstance, List<BattleWreckage.WreckageType>>();
        foreach (var client in Clients)
        {
            var types = new List<BattleWreckage.WreckageType>();
            typesWhenAnnounced[client] = types;

            client.Call(() =>
            {
                CampaignEvents.MapInteractableCreated.AddNonSerializedListener(types, point =>
                {
                    if (point is BattleWreckage battleWreckage) types.Add(battleWreckage.WreckageTypeCategory);
                });
            });
        }

        // Deliver on the poller thread like a live client, so each handler's game thread work queues up behind the
        // deferred create and field values instead of running inline
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        // Act
        CreateServerWreckage();

        foreach (var client in Clients) client.PumpGameThread();

        // Assert
        foreach (var client in Clients)
        {
            var type = Assert.Single(typesWhenAnnounced[client]);
            Assert.Equal(BattleWreckage.WreckageType.Small, type);
        }
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncsRosters()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        // Act
        var serverRosterIds = new List<string>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            battleWreckage.AttackerDiedInBattle.AddToCounts(character, 3);

            foreach (var roster in GetRosters(battleWreckage))
            {
                Assert.True(Server.ObjectManager.TryGetId(roster, out var rosterId));
                serverRosterIds.Add(rosterId);
            }
        });

        // Ownerless roster changes are coalesced and sent on the server's next tick
        TestEnvironment.FlushCoalescer();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

                var clientRosterIds = new List<string>();
                foreach (var roster in GetRosters(battleWreckage))
                {
                    Assert.True(client.ObjectManager.TryGetId(roster, out var rosterId));
                    clientRosterIds.Add(rosterId);
                }

                Assert.Equal(serverRosterIds, clientRosterIds);
                Assert.Equal(3, battleWreckage.AttackerDiedInBattle.TotalManCount);
            });
        }
    }

    [Fact]
    public void ServerDestroy_BattleWreckage_SyncAllClients()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        var clientWreckages = new Dictionary<EnvironmentInstance, BattleWreckage>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                clientWreckages[client] = battleWreckage;
            });
        }

        // The map visual is removed on MapInteractableDestroyed
        var destroyedWreckages = new Dictionary<EnvironmentInstance, List<BattleWreckage>>();
        foreach (var client in Clients)
        {
            var destroyed = new List<BattleWreckage>();
            destroyedWreckages[client] = destroyed;

            client.Call(() =>
            {
                CampaignEvents.MapInteractableDestroyed.AddNonSerializedListener(destroyed, point =>
                {
                    if (point is BattleWreckage battleWreckage) destroyed.Add(battleWreckage);
                });
            });
        }

        // Deliver on the poller thread like a live client, so the remove and the registry's destroy queue on the game
        // thread in send order, and the remove has to find the wreckage before the destroy drops its id
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            battleWreckage.DestroyWreckage();
        });

        foreach (var client in Clients) client.PumpGameThread();

        // Assert
        foreach (var client in Clients)
        {
            Assert.Same(clientWreckages[client], Assert.Single(destroyedWreckages[client]));

            client.Call(() =>
            {
                Assert.False(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out _));
                Assert.DoesNotContain(clientWreckages[client], Campaign.Current.Wreckages);
            });
        }
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

            // Epic wreckages stay on the map once investigated, so clients still have one to check. Creating one as Epic
            // needs a settlement for its name, and the field is readonly
            AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory))
                .SetValue(battleWreckage, BattleWreckage.WreckageType.Epic);

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
            Assert.True(decision.RequiredInvestigationDuration.ToHours >= 2);
            Assert.Equal(decision.RequiredInvestigationDuration, encounter.RequiredInvestigationDuration);
            Assert.Equal(BattleWreckageCampaignBehavior.RecoverTroopsConsequenceId, encounter.OverriddenConsequenceId);
            Assert.Empty(encounter.ConsequenceExplanations);

            // Act, the accepted recovery rolls the troops
            campaignBehaviorInterface.AcceptRecoverTroops(hero);
            campaignBehaviorInterface.SelectWreckageConsequences(hero);

            // Assert
            Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
            Assert.Single(Server.InternalMessages.GetMessages<WreckageConsequencesSelected>());
            Assert.True(encounter.LootedTroops.TotalManCount > 0);
            Assert.All(encounter.LootedTroops.GetTroopRoster(), element => Assert.Same(woundedTroop, element.Character));
            Assert.Single(encounter.ConsequenceExplanations);

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

            // Act, declining sends another select without accepting first
            campaignBehaviorInterface.SelectWreckageConsequences(hero);
            campaignBehaviorInterface.SelectWreckageConsequences(hero);

            // Assert
            Assert.Single(Server.InternalMessages.GetMessages<RecoverTroopsDecisionRequired>());
            Assert.Single(Server.InternalMessages.GetMessages<WreckageConsequencesSelected>());
            Assert.Equal(0, encounter.LootedTroops.TotalManCount);
            Assert.Single(encounter.ConsequenceExplanations);
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
            Assert.False(encounter.IsInterrupted);

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
            Assert.True(encounter.IsInterrupted);
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

            // Creating one as Epic needs a settlement for its name, and the field is readonly
            AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory)).SetValue(battleWreckage, wreckageType);

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
    public void ServerStart_InvestigatedEpicRemains_LetsSeveralPlayersView()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            // Creating one as Epic needs a settlement for its name, and the field is readonly
            AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory))
                .SetValue(battleWreckage, BattleWreckage.WreckageType.Epic);
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

        var encounter = new BattleWreckageEncounter(hero, battleWreckage);
        Server.Resolve<IBattleWreckageEncounterTracker>().AddEncounter(encounter);

        return (hero, encounter, behavior, woundedTroop);
    }

    private static BattleWreckageCampaignBehavior AddBattleWreckageBehavior()
    {
        var behavior = new BattleWreckageCampaignBehavior();
        Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { behavior }));
        return behavior;
    }

    // The host has no party, so vanilla's MainParty and MainHero reads come from this party under substitution
    private static Hero CreatePartyLeader()
    {
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        return hero;
    }

    private string CreateServerWreckage()
    {
        string? battleWreckageId = null;

        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            SetSides(mapEvent,
                GameObjectCreator.CreateInitializedObject<MobileParty>(),
                GameObjectCreator.CreateInitializedObject<MobileParty>());

            BattleWreckage.CreateWreckage(mapEvent, BattleWreckage.WreckageType.Small, CampaignTime.Zero);

            var battleWreckage = Campaign.Current.Wreckages.Last();

            Assert.True(Server.ObjectManager.TryGetId(battleWreckage, out battleWreckageId));
        });

        Assert.NotNull(battleWreckageId);

        return battleWreckageId!;
    }

    // The wreckage constructor reads both sides' leader parties and their names. Custom names skip the
    // lord party name lookup, which needs a fully built leader hero
    private static void SetSides(MapEvent mapEvent, MobileParty attackerParty, MobileParty defenderParty)
    {
        attackerParty.Party.SetCustomName(new TextObject("Attackers"));
        defenderParty.Party.SetCustomName(new TextObject("Defenders"));

        mapEvent._sides[(int)BattleSideEnum.Attacker] = new MapEventSide(mapEvent, BattleSideEnum.Attacker, attackerParty.Party);
        mapEvent._sides[(int)BattleSideEnum.Defender] = new MapEventSide(mapEvent, BattleSideEnum.Defender, defenderParty.Party);
    }

    private static TroopRoster[] GetRosters(BattleWreckage battleWreckage) => new[]
    {
        battleWreckage.AttackerDiedInBattle,
        battleWreckage.DefenderDiedInBattle,
        battleWreckage.AttackerWoundedInBattle,
        battleWreckage.DefenderWoundedInBattle,
    };
}
