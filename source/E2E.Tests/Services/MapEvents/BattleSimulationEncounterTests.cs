using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedAdvance_NonCaptiveDefeat_CanLeaveAfterScoreboard(bool menuConsequence)
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
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                    System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                    context.MapEventId, context.AttackerPartyId));
                Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
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

                if (menuConsequence)
                    new TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior()
                        .game_menu_encounter_leave_on_consequence(null);
                else
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
    public void NativeScoreboardRetreat_CancelsWithoutEndingMapEvent_AndCanResume()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), context.AttackerPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", client.NetPeer);
        var ended = new List<MapEvent>();
        Server.Call(() =>
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                context.MapEventId, context.AttackerPartyId));
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
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
                var inquiries = new List<InquiryData>();
                void RecordInquiry(InquiryData inquiry, bool pause, bool prioritize) => inquiries.Add(inquiry);
                // Publicizer exposes both the event and its backing field under the same name.
                var showInquiry = typeof(InformationManager).GetEvent("OnShowInquiry");
                showInquiry.AddEventHandler(null, (System.Action<InquiryData, bool, bool>)RecordInquiry);
                try
                {
                    simulation.Pause();
                    scoreboard.ExecuteQuitAction();
                    Assert.False(simulation.IsPlayerRetreated);
                    Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                    Assert.Single(inquiries).AffirmativeAction();
                    Assert.True(simulation.IsPlayerRetreated);
                    Assert.False(mapState.IsSimulationActive);
                    Assert.Single(client.NetworkSentMessages.GetMessages<NetworkCancelBattleSimulation>());
                }
                finally
                {
                    showInquiry.RemoveEventHandler(null, (System.Action<InquiryData, bool, bool>)RecordInquiry);
                }
            }, MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu))));

            Server.Call(() =>
            {
                var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
                Assert.False(mapEvent.IsFinalized);
                Assert.False(mapEvent.HasWinner);
                Assert.Empty(ended);
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationCancelled>());
                Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
                Assert.False(ServerBattleModeArbiter.IsClaimed(context.MapEventId));

                Server.NetworkSentMessages.Clear();
                Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                    System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                    context.MapEventId, context.AttackerPartyId));
                Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
                Assert.True(ServerBattleModeArbiter.IsClaimed(context.MapEventId));
                Assert.Empty(ended);
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => CampaignEvents.MapEventEnded.ClearListeners(this));
        }
    }
}
