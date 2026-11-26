using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using Missions.Messages;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>Exercises client encounter exit after authoritative simulation completion.</summary>
public class BattleSimulationEncounterTests : MapEventTestBase
{
    public BattleSimulationEncounterTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DestroyedEvent_BeforePlaybackFinishes_ScoreboardTickKeepsPresentationOpen()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        SPScoreboardVM scoreboard = null;
        BattleSimulation simulation = null;
        string playerPartyId = null;
        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            Assert.True(client.ObjectManager.TryGetId(MobileParty.MainParty.Party, out playerPartyId));
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            simulation = new BattleSimulation(null, null);
            encounter.BattleSimulation = simulation;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = simulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            BattleSimulationReplay.Begin(context.MapEventId);
            scoreboard = SPScoreboardVM.CreateSimulation(simulation);
            scoreboard.IsSimulation = true;
            Assert.False(scoreboard.IsOver);
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkDestroyInstance<MapEvent>(
            client.GetHandle<MapEvent>(context.MapEventId)));
        client.SimulateMessage(Server.NetPeer, new NetworkClosePvpEncounter(
            new[] { playerPartyId }, mapEventId: context.MapEventId));

        client.Call(() =>
        {
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.False(scoreboard.IsOver);
            Assert.False(simulation.IsSimulationFinished);
            // The view can tick between event destruction and the final playback message.
            // OnTick is protected in the game assembly used by the test project.
            using var moraleReads = new MethodCallRecorder(Priority.First, true,
                AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.GetSideMorale)));
            var tick = AccessTools.Method(typeof(SPScoreboardVM), "OnTick");
            const int ScoreboardTicks = 200;
            const float TickSeconds = 1.1f; // Match the game tick duration used while the scoreboard is displayed.
            for (var frame = 0; frame < ScoreboardTicks; frame++)
                tick.Invoke(scoreboard, new object[] { TickSeconds });
            Assert.Equal(0, moraleReads.CountFor(client));
            Assert.True(Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().IsSimulationActive);
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationFinished(context.MapEventId));
        client.Call(() =>
        {
            BattleSimulationReplay.Tick(simulation, 0f);
            Assert.True(simulation.IsSimulationFinished);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CompletedAdvance_NonCaptiveDefeat_CanLeaveAfterScoreboard()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            encounter._attackerParty = MobileParty.MainParty.Party;
            encounter._defenderParty = encounter._encounteredParty;
            encounter.PlayerSide = BattleSideEnum.Attacker;
            encounter.EncounterState = PlayerEncounterState.End;
            encounter.BattleSimulation = new BattleSimulation(null, null);
            encounter.BattleSimulation.IsSimulationFinished = true;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = encounter.BattleSimulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            Assert.False(PlayerCaptivity.IsCaptive);
        }, MapEventDisabledMethods);

        var ended = new List<MapEvent>();
        try
        {
            Server.Call(() =>
            {
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
                StartAcceptedSimulation(client, context);
                Server.GetRegisteredObject<MapEvent>(context.MapEventId).BattleState = BattleState.DefenderVictory;
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(context.MapEventId, 1));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Single(ended);
            }, MapEventDisabledMethods);

            client.Call(() =>
            {
                var encounter = PlayerEncounter.Current;
                Assert.NotNull(encounter);
                var simulation = encounter.BattleSimulation;
                Game.Current.GameStateManager.GameStates.OfType<MapState>().Single().EndBattleSimulation();
                simulation.OnFinished();
                PlayerEncounter.Update();
                Assert.Equal(PlayerEncounterState.Begin, encounter.EncounterState);
                Assert.Null(encounter._mapEvent);
                Assert.Null(encounter.BattleSimulation);

                MenuHelper.EncounterLeaveConsequence();

                Assert.Null(PlayerEncounter.Current);
                Assert.Null(MobileParty.MainParty.Party.MapEventSide);
            }, MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)))
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)))
                // Headless campaigns have no navigation mesh for vanilla's post-encounter displacement.
                .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius))));
            Server.Call(() => Assert.Single(ended));
        }
        finally
        {
            Server.Call(() => CampaignEvents.MapEventEnded.ClearListeners(this));
        }
    }

    [Fact]
    public void NativeScoreboardRetreat_FinalizesLeaderMapEvent_AndClosesSimulation()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        var ended = new List<MapEvent>();
        Server.Call(() =>
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            StartAcceptedSimulation(client, context);
        }, MapEventDisabledMethods);

        try
        {
            client.Call(() =>
            {
                Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
                var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
                var simulation = new BattleSimulation(null, null);
                encounter.BattleSimulation = simulation;
                var mapState = Game.Current.GameStateManager.CreateState<MapState>();
                mapState._battleSimulation = simulation;
                Game.Current.GameStateManager._gameStates.Add(mapState);
                BattleSimulationReplay.Begin(context.MapEventId, spectator: false);

                var scoreboard = SPScoreboardVM.CreateSimulation(simulation);
                scoreboard.IsSimulation = true;
                // ScoreboardBaseVM is not publicized; replace only the renderer's release callback.
                AccessTools.Field(typeof(ScoreboardBaseVM), "_releaseSimulationSources")
                    .SetValue(scoreboard, (System.Action)mapState.EndBattleSimulation);
                using (var inquiries = new InquiryRecorder())
                {
                    simulation.Pause();
                    scoreboard.ExecuteQuitAction();
                    Assert.False(simulation.IsPlayerRetreated);
                    Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                    Assert.Single(inquiries.Items).AffirmativeAction();
                    Assert.True(simulation.IsPlayerRetreated);
                    Assert.False(mapState.IsSimulationActive);
                    Assert.Single(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                }
            }, MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu))));

            Server.Call(() =>
            {
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationCancelled>());
                Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Single(ended);
                Assert.False(ServerBattleModeArbiter.IsClaimed(context.MapEventId));
            }, MapEventDisabledMethods);

            client.Call(() =>
            {
                client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationCancelled(context.MapEventId));
                var mapState = Game.Current.GameStateManager.GameStates.OfType<MapState>().Single();
                Assert.False(mapState.IsSimulationActive);
                Assert.Null(PlayerEncounter.Current?.BattleSimulation);
                Assert.False(BattleSimulationReplay.IsActiveFor(context.MapEventId));
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => CampaignEvents.MapEventEnded.ClearListeners(this));
        }
    }

    [Fact]
    public void ServerCancellation_ClosesInitiatingSimulation()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        Server.Call(() =>
        {
            StartAcceptedSimulation(client, context);
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(context.AttackerPartyId);
            var encounter = SetMockPlayerEncounter(client, context.DefenderPartyId, context.MapEventId);
            var simulation = new BattleSimulation(null, null);
            encounter.BattleSimulation = simulation;
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._battleSimulation = simulation;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            BattleSimulationReplay.Begin(context.MapEventId, spectator: false);

            client.SimulateMessage(Server.NetPeer, new NetworkBattleSimulationCancelled(context.MapEventId));

            Assert.False(mapState.IsSimulationActive);
            Assert.Null(encounter.BattleSimulation);
            Assert.False(BattleSimulationReplay.IsActiveFor(context.MapEventId));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void RetreatedDefender_LosesSendTroops_IsTakenPrisonerOffTheEncounterMenu()
    {
        const string playerId = "PlayerOne";
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(playerId);
        var (heroId, partyId) = CreatePlayerHeroParty(playerId);
        Server.Resolve<IPlayerManager>().SetPeer(playerId, client.NetPeer);
        var captorId = CreateServerParty();
        var mapEventId = CreateBattleStateDefeatMapEvent(heroId, partyId, captorId);

        Server.Call(() =>
        {
            EnsureEncounterModel();
            Campaign.Current.MapEventManager.OnMapEventCreated(Server.GetRegisteredObject<MapEvent>(mapEventId));
            // The knocked out player retreated from the mission before sending the troops.
            Server.Resolve<IMessageBroker>().Publish(this, new BattlePartyRetreated(playerId, mapEventId));
        }, MapEventDisabledMethods);

        BattleSimulation simulation = null;
        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            var captor = client.GetRegisteredObject<MobileParty>(captorId);
            var mapEvent = client.GetRegisteredObject<MapEvent>(mapEventId);
            Campaign.Current.MainParty = party;
            Campaign.Current.MapStateData ??= new MapStateData();
            Assert.Same(mapEvent, party.MapEvent);

            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            new EncounterGameMenuBehavior().OnSessionLaunched(starter);
            // PlayerTownVisitCampaignBehavior registers prisoner_wait in game; an empty wait menu is enough here.
            starter.AddWaitGameMenu("prisoner_wait", "", null, null, null, null, GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = captor.Party;
            encounter._attackerParty = captor.Party;
            encounter._defenderParty = party.Party;
            encounter._alternativeReceivedLootShips = new List<Ship>();
            encounter.PlayerSide = BattleSideEnum.Defender;
            encounter.OpponentSide = BattleSideEnum.Attacker;
            encounter.EncounterState = PlayerEncounterState.Wait;
            Campaign.Current.PlayerEncounter = encounter;
            Game.Current.GameStateManager._gameStates.Add(Game.Current.GameStateManager.CreateState<MapState>());

            // E2E clients share process statics, so select this client's coordinator for the patch.
            AccessTools.Field(typeof(BattleStartCoordinator), "<Instance>k__BackingField")
                .SetValue(null, client.Resolve<BattleStartCoordinator>());
            AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_encounter_order_attack_on_consequence")
                .Invoke(new EncounterGameMenuBehavior(), new object[] { new MenuCallbackArgs((MenuContext)null, null) });

            simulation = encounter.BattleSimulation;
            Assert.NotNull(simulation);
            Assert.True(BattleSimulationReplay.IsActiveFor(mapEventId));
        }, MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(BeHostileAction), nameof(BeHostileAction.ApplyEncounterHostileAction)))
            // The client's local troop setup reads the engine config the harness lacks; the server owns the simulation.
            .Append(AccessTools.Method(typeof(BattleSimulation), nameof(BattleSimulation.ResetSimulation))));

        // Every sent troop died, so the attackers win the server's simulation on the next client paced advance.
        Server.Call(() => Server.GetRegisteredObject<MapEvent>(mapEventId).BattleState = BattleState.AttackerVictory,
            BattleResultDisabledMethods);
        client.Call(() => BattleSimulationReplay.Tick(simulation, 1f), BattleResultDisabledMethods);

        Server.Call(() =>
        {
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
            Assert.Same(Server.GetRegisteredObject<MobileParty>(captorId).Party,
                Server.GetRegisteredObject<Hero>(heroId).PartyBelongedToAsPrisoner);
        });

        client.Call(() =>
        {
            BattleSimulationReplay.Tick(simulation, 0f);
            Assert.True(simulation.IsSimulationFinished);

            // The scoreboard's Done closes the simulation and reopens the encounter menu.
            var mapState = Game.Current.GameStateManager.GameStates.OfType<MapState>().Single();
            mapState.EndBattleSimulation();
            simulation.OnFinished();

            Assert.True(PlayerCaptivity.IsCaptive);
            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.Equal("prisoner_wait", mapState.MenuContext?.GameMenu?.StringId);
        }, MapEventDisabledMethods);
    }

    private IReadOnlyList<MethodBase> BattleResultDisabledMethods => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
        .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
        .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
        .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
        .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
        .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
        .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
        // Headless campaigns have no navigation mesh for the finalize displacement of a defeated party.
        .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
        .ToList();

    private sealed class InquiryRecorder : IDisposable
    {
        private readonly EventInfo showInquiry = typeof(InformationManager).GetEvent("OnShowInquiry");
        private readonly Action<InquiryData, bool, bool> record;
        public List<InquiryData> Items { get; } = new();

        public InquiryRecorder()
        {
            record = (inquiry, _, _) => Items.Add(inquiry);
            showInquiry.AddEventHandler(null, record);
        }

        public void Dispose() => showInquiry.RemoveEventHandler(null, record);
    }
}
