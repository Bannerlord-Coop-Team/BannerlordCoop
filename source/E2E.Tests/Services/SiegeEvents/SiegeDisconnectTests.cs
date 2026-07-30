using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeDisconnectTests : MapEventTestBase
{
    private static IReadOnlyList<MethodBase> SiegeCreationDisabledMethods => new[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    public SiegeDisconnectTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void SiegeLeaderDisconnects_PreservesAttachedFollowerAndLaterSnapshot()
    {
        var disconnectedClient = Clients.First();
        var (_, disconnectedPartyId) = CreatePlayerHeroParty("SiegeLeader");
        var (_, remainingPartyId) = CreatePlayerHeroParty("SiegeMember");
        var siege = SetupSiege(disconnectedPartyId, remainingPartyId);
        AttachFollower(disconnectedPartyId, remainingPartyId);

        DisconnectPlayer(disconnectedClient, "SiegeLeader");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(disconnectedPartyId, out var disconnectedParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(remainingPartyId, out var remainingParty));
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(siege.CampId, out var camp));

            Assert.Null(disconnectedParty.BesiegerCamp);
            Assert.False(disconnectedParty.IsActive);
            Assert.Same(camp, remainingParty.BesiegerCamp);
            Assert.Same(remainingParty, camp.LeaderParty);
            Assert.Equal(new[] { remainingParty }, camp._besiegerParties);
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(disconnectedPartyId, out var disconnectedParty));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(remainingPartyId, out var remainingParty));
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(siege.CampId, out var camp));

                Assert.Null(disconnectedParty.BesiegerCamp);
                Assert.Same(camp, remainingParty.BesiegerCamp);
                Assert.Same(remainingParty, camp.LeaderParty);
                Assert.Equal(new[] { remainingParty }, camp._besiegerParties);
            });
        }

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
            siegeEvent.FinalizeSiegeEvent();
        });

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Equal(remainingPartyId, prompt.LeaderPartyId);
        Assert.Equal(new[] { remainingPartyId }, prompt.AttackerPartyIds);
        Assert.Empty(prompt.DefenderPartyIds);
    }

    [Fact]
    public void SoleSiegeLeaderDisconnects_FinalizesEmptyCampWithLeaderRoleSnapshot()
    {
        var disconnectedClient = Clients.First();
        var (_, disconnectedPartyId) = CreatePlayerHeroParty("SoleSiegeLeader");
        var siege = SetupSiege(disconnectedPartyId);

        Server.Call(() =>
        {
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
        });
        DisconnectPlayer(disconnectedClient, "SoleSiegeLeader");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(disconnectedPartyId, out var party));
            Assert.Null(party.BesiegerCamp);
            Assert.False(party.IsActive);
            Assert.False(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        });

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, disconnectedPartyId, expectCamp: false);
            Assert.False(client.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        }

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Equal(disconnectedPartyId, prompt.LeaderPartyId);
        Assert.Empty(prompt.AttackerPartyIds);
        Assert.Empty(prompt.DefenderPartyIds);
    }

    [Fact]
    public void SiegePlayerDisconnectsDuringBattle_ClearsCampWhenBattleFinalizesBeforeParking()
    {
        var disconnectedClient = Clients.First();
        var (_, disconnectedPartyId) = CreatePlayerHeroParty("SiegeBattlePlayer");
        var siege = SetupSiege(disconnectedPartyId);
        var mapEvent = CreateServerMapEvent();

        JoinPartyToMapEvent(disconnectedPartyId, mapEvent.MapEventId);
        DisconnectPlayer(disconnectedClient, "SiegeBattlePlayer");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(disconnectedPartyId, out var party));
            Assert.NotNull(party.MapEvent);
            Assert.NotNull(party.BesiegerCamp);
            Assert.True(party.IsActive);
        });

        DestroyServerMapEvent(mapEvent.MapEventId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(disconnectedPartyId, out var party));
            Assert.Null(party.MapEvent);
            Assert.Null(party.BesiegerCamp);
            Assert.False(party.IsActive);
            Assert.False(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        });

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, disconnectedPartyId, expectCamp: false);
            Assert.False(client.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        }
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    public void BreakSiegeRequest_WithNoCampDuringAssault_LeavesBattleAndHonorsMenuContinuation(
        bool finishLocalMenus,
        bool hasPlayerEncounter,
        int expectedCleanupCalls)
    {
        var requestingClient = Clients.First();
        var (_, partyId) = CreatePlayerHeroParty("SiegeAssaultPlayer");
        var mapEvent = CreateServerMapEvent();

        JoinPartyToMapEvent(partyId, mapEvent.MapEventId);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var activeMapEvent));
                activeMapEvent._mapEventType = MapEvent.BattleTypes.Siege;
            });
        }
        ConfigureStaleSiegeMenu(
            requestingClient,
            partyId,
            mapEvent.MapEventId,
            hasPlayerEncounter);

        using var menuCalls = new GameMenuCallCounter();
        Server.SimulateMessage(
            requestingClient.NetPeer,
            new NetworkRequestBreakSiege(partyId, finishLocalMenus));

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.Equal(SiegeBreakOutcome.Applied, approval.Outcome);
        Assert.Equal(finishLocalMenus, approval.FinishLocalMenus);
        Assert.True(approval.BattleLeaveApplied);
        Assert.Equal(expectedCleanupCalls, menuCalls.ExitCountFor(requestingClient));
        Assert.Equal(expectedCleanupCalls, menuCalls.DeactivationCountFor(requestingClient));
        Assert.Equal(0, menuCalls.ExitCountFor(Clients.Last()));
        Assert.Equal(0, menuCalls.DeactivationCountFor(Clients.Last()));

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Null(party.MapEvent);
            });
        }

        requestingClient.Call(() =>
            Assert.Equal(
                hasPlayerEncounter && !finishLocalMenus,
                PlayerEncounter.Current != null));
        Clients.Last().Call(() => Assert.Null(PlayerEncounter.Current));
    }

    [Fact]
    public void NetworkPartyLeftBattle_FieldBattleWithoutEncounter_PreservesLocalMenuState()
    {
        const string fieldBattleMenuId = "field_battle_menu";
        var client = Clients.First();
        var (_, partyId) = CreatePlayerHeroParty("FieldBattlePlayer");
        var mapEvent = CreateServerMapEvent();
        string? partyBaseId = null;

        JoinPartyToMapEvent(partyId, mapEvent.MapEventId);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetId(party.Party, out partyBaseId));
            Assert.NotNull(party.MapEvent);
            Assert.True(party.MapEvent.IsFieldBattle);

            Campaign.Current.MainParty = party;
            Campaign.Current.PlayerEncounter = null;
            Campaign.Current.MapStateData.GameMenuId = fieldBattleMenuId;

            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            Game.Current.GameStateManager._gameStates.Add(mapState);

            Assert.Null(PlayerEncounter.Current);
            Assert.Null(Campaign.Current.CurrentMenuContext);
        });
        Assert.NotNull(partyBaseId);

        using var menuCalls = new GameMenuCallCounter();
        var leave = new NetworkPartyLeftBattle(partyBaseId!);
        Assert.True(leave.FinishLocalMenus);
        client.SimulateMessage(this, leave);

        Assert.Equal(0, menuCalls.ExitCountFor(client));
        Assert.Equal(0, menuCalls.DeactivationCountFor(client));
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Null(party.MapEvent);
            Assert.Null(PlayerEncounter.Current);
            Assert.Equal(fieldBattleMenuId, Campaign.Current.MapStateData.GameMenuId);
        });
    }

    [Fact]
    public void SiegeTerminationSnapshot_BroadcastsLeaderMemberAndDefenderRoles()
    {
        var (_, leaderPartyId) = CreatePlayerHeroParty("TerminationLeader");
        var (_, memberPartyId) = CreatePlayerHeroParty("TerminationMember");
        var (_, defenderPartyId) = CreatePlayerHeroParty("TerminationDefender");
        var siege = SetupSiege(leaderPartyId, memberPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leader));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(memberPartyId, out var member));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defender));

            Server.Resolve<IMessageBroker>().Publish(
                this,
                new SiegeEndedWithoutBattle(
                    siegeEvent.BesiegedSettlement,
                    besiegerDefeated: false,
                    leader,
                    new[] { leader, member },
                    new[] { defender }));
        });

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Equal(leaderPartyId, prompt.LeaderPartyId);
        Assert.Equal(new[] { leaderPartyId, memberPartyId }, prompt.AttackerPartyIds);
        Assert.Equal(new[] { defenderPartyId }, prompt.DefenderPartyIds);
    }

    [Fact]
    public void SiegeFinalizesDuringPreparation_BroadcastsOneCompleteParticipantSnapshot()
    {
        var (_, leaderPartyId) = CreatePlayerHeroParty("PeaceLeader");
        var (_, memberPartyId) = CreatePlayerHeroParty("PeaceMember");
        var siege = SetupSiege(leaderPartyId, memberPartyId);

        Server.Call(() =>
        {
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
            siegeEvent.FinalizeSiegeEvent();
        });

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Equal(leaderPartyId, prompt.LeaderPartyId);
        Assert.Equal(new[] { leaderPartyId, memberPartyId }, prompt.AttackerPartyIds);
        Assert.Empty(prompt.DefenderPartyIds);

        foreach (var instance in Clients.Append(Server))
        {
            AssertBesiegerCamp(instance, leaderPartyId, expectCamp: false);
            AssertBesiegerCamp(instance, memberPartyId, expectCamp: false);
            Assert.False(instance.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out _));
        }
    }

    [Fact]
    public void SiegeFinalizesAfterPeace_IncludesPlayerDefenderInsideSettlement()
    {
        var (_, leaderPartyId) = CreatePlayerHeroParty("PeaceAttacker");
        var (_, defenderPartyId) = CreatePlayerHeroParty("PeaceDefender");
        var factionId = TestEnvironment.CreateRegisteredObject<Clan>();
        var siege = SetupSiege(leaderPartyId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defender));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(factionId, out var faction));

                leader.ActualClan = faction;
                defender.ActualClan = faction;
                defender._currentSettlement = siegeEvent.BesiegedSettlement;
                siegeEvent.BesiegedSettlement._partiesCache.Add(defender);
                siegeEvent.BesiegerCamp._faction = faction;
            });
        }

        Server.Call(() =>
        {
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siege.SiegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defender));

            Assert.False(defender.MapFaction.IsAtWarWith(siegeEvent.BesiegerCamp.MapFaction));

            siegeEvent.FinalizeSiegeEvent();
        });

        var prompt = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeEnded>());
        Assert.Equal(leaderPartyId, prompt.LeaderPartyId);
        Assert.Equal(new[] { leaderPartyId }, prompt.AttackerPartyIds);
        Assert.Equal(new[] { defenderPartyId }, prompt.DefenderPartyIds);
    }

    [Theory]
    [InlineData(SiegeTerminationRole.AttackerLeader, null, true)]
    [InlineData(SiegeTerminationRole.AttackerMember, "army_wait", false)]
    [InlineData(SiegeTerminationRole.Defender, "siege_attacker_left", false)]
    public void SiegeTerminationPrompt_UsesRoleAppropriateMenuDisposition(
        SiegeTerminationRole role,
        string? expectedMenu,
        bool expectExit)
    {
        var client = Clients.First();
        var (_, mainPartyId) = CreatePlayerHeroParty("TerminationClient");
        var leaderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        ConfigureTerminationClient(client, mainPartyId, leaderPartyId, settlementId, role);

        using var menuCalls = new GameMenuCallCounter();
        client.SimulateMessage(
            this,
            new NetworkPromptSiegeEnded(
                settlementId,
                besiegerDefeated: false,
                role == SiegeTerminationRole.AttackerLeader ? mainPartyId : leaderPartyId,
                role == SiegeTerminationRole.AttackerMember
                    ? new[] { mainPartyId }
                    : Array.Empty<string>(),
                role == SiegeTerminationRole.Defender
                    ? new[] { mainPartyId }
                    : Array.Empty<string>()));

        Assert.Equal(expectExit ? 1 : 0, menuCalls.ExitCountFor(client));
        if (expectedMenu == null)
            Assert.Empty(menuCalls.SwitchesFor(client));
        else
            Assert.Equal(new[] { expectedMenu }, menuCalls.SwitchesFor(client));
    }

    private SiegeContext SetupSiege(params string[] attackerPartyIds)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        ConfigureSiegeTeardownModels();
        string? campId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out campId));
        });
        Assert.NotNull(campId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(campId!, out var camp));
                camp._besiegerParties.Clear();

                foreach (var partyId in attackerPartyIds)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    if (party.Ai == null)
                    {
                        using (new AllowedThread())
                        {
                            party.Ai = new MobilePartyAi(party);
                        }
                    }

                    party._besiegerCamp = camp;
                    camp._besiegerParties.Add(party);
                }

                camp._leaderParty = camp._besiegerParties[0];
                camp._faction = camp._leaderParty.MapFaction;
            });
        }

        return new SiegeContext(siegeEventId, campId);
    }

    private void ConfigureSiegeTeardownModels()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var models = Campaign.Current.Models.GetGameModels().ToList();
                models.Add(new DefaultEncounterModel());
                models.Add(new DefaultPartyImpairmentModel());
                models.Add(new DefaultPartyNavigationModel());
                var gameModels = new GameModels(models);

                instance.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
                Campaign.Current._gameModels = gameModels;
            });
        }
    }

    private void DisconnectPlayer(
        EnvironmentInstance disconnectedClient,
        string controllerId)
    {
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer(controllerId, disconnectedClient.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerDisconnected(disconnectedClient.NetPeer, default));
        });
    }

    private void JoinPartyToMapEvent(string partyId, string mapEventId)
    {
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            party.Party.MapEventSide = mapEvent.AttackerSide;
        }, disabledMethods);
    }

    private static void ConfigureTerminationClient(
        EnvironmentInstance client,
        string mainPartyId,
        string leaderPartyId,
        string settlementId,
        SiegeTerminationRole role)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Campaign.Current.MainParty = mainParty;
            if (role == SiegeTerminationRole.Defender)
                mainParty._currentSettlement = settlement;

            if (role == SiegeTerminationRole.AttackerMember)
            {
                var army = ObjectHelper.SkipConstructor<Army>();
                army.LeaderParty = leaderParty;
                mainParty._army = army;
            }

            var gameStateManager = Game.Current.GameStateManager;
            var mapState = gameStateManager.CreateState<MapState>();
            var menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            menuContext.GameMenu = new GameMenu("menu_siege_strategies");
            mapState._menuContext = menuContext;
            gameStateManager._gameStates.Add(mapState);
        });
    }

    private void AttachFollower(string leaderPartyId, string followerPartyId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(followerPartyId, out var follower));

                leader._attachedParties.Add(follower);
                follower._attachedTo = leader;
            });
        }
    }

    private static void ConfigureStaleSiegeMenu(
        EnvironmentInstance client,
        string mainPartyId,
        string mapEventId,
        bool hasPlayerEncounter)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Campaign.Current.MainParty = mainParty;
            Campaign.Current.PlayerEncounter = null;
            if (hasPlayerEncounter)
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                PlayerEncounter.Start();
                PlayerEncounter.Current._mapEvent = mapEvent;
            }

            var gameStateManager = Game.Current.GameStateManager;
            var mapState = gameStateManager.CreateState<MapState>();
            var menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            menuContext.GameMenu = new GameMenu("menu_siege_strategies");
            mapState._menuContext = menuContext;
            gameStateManager._gameStates.Add(mapState);
        });
    }

    private static void AssertBesiegerCamp(
        EnvironmentInstance instance,
        string partyId,
        bool expectCamp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(expectCamp, party.BesiegerCamp != null);
        });
    }

    private sealed class GameMenuCallCounter : IDisposable
    {
        private static readonly MethodInfo ExitToLastMethod =
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast));
        private static readonly MethodInfo SwitchToMenuMethod =
            AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu), new[] { typeof(string) });
        private static readonly MethodInfo PlayerSiegeDeactivatedMethod =
            AccessTools.Method(typeof(MapState), nameof(MapState.OnPlayerSiegeDeactivated));
        private static readonly List<object> ExitContainers = new();
        private static readonly List<(object Container, string MenuId)> SwitchCalls = new();
        private static readonly List<object> DeactivationContainers = new();

        private readonly Harmony harmony = new($"siege-termination-menu-counter-{Guid.NewGuid()}");

        public GameMenuCallCounter()
        {
            ExitContainers.Clear();
            SwitchCalls.Clear();
            DeactivationContainers.Clear();
            harmony.Patch(
                ExitToLastMethod,
                prefix: new HarmonyMethod(typeof(GameMenuCallCounter), nameof(CountExitToLast)));
            harmony.Patch(
                SwitchToMenuMethod,
                prefix: new HarmonyMethod(typeof(GameMenuCallCounter), nameof(CountSwitchToMenu)));
            harmony.Patch(
                PlayerSiegeDeactivatedMethod,
                prefix: new HarmonyMethod(typeof(GameMenuCallCounter), nameof(CountPlayerSiegeDeactivated)));
        }

        public int ExitCountFor(EnvironmentInstance instance) =>
            ExitContainers.Count(container => ReferenceEquals(container, instance.Container));

        public string[] SwitchesFor(EnvironmentInstance instance) =>
            SwitchCalls
                .Where(call => ReferenceEquals(call.Container, instance.Container))
                .Select(call => call.MenuId)
                .ToArray();

        public int DeactivationCountFor(EnvironmentInstance instance) =>
            DeactivationContainers.Count(container => ReferenceEquals(container, instance.Container));

        public void Dispose()
        {
            harmony.Unpatch(ExitToLastMethod, HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(SwitchToMenuMethod, HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(PlayerSiegeDeactivatedMethod, HarmonyPatchType.Prefix, harmony.Id);
        }

        private static bool CountExitToLast()
        {
            if (GameInterface.ContainerProvider.TryGetContainer(out var container))
                ExitContainers.Add(container);
            return false;
        }

        private static bool CountSwitchToMenu(string menuId)
        {
            if (GameInterface.ContainerProvider.TryGetContainer(out var container))
                SwitchCalls.Add((container, menuId));
            return false;
        }

        private static bool CountPlayerSiegeDeactivated()
        {
            if (GameInterface.ContainerProvider.TryGetContainer(out var container))
                DeactivationContainers.Add(container);
            return false;
        }
    }

    private readonly record struct SiegeContext(string SiegeEventId, string CampId);
}
