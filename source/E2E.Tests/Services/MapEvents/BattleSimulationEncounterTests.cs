using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkBattleStartRequest(
                System.Guid.NewGuid().ToString(), (int)BattleStartMode.Simulation,
                context.MapEventId, context.AttackerPartyId));
            Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
            Server.GetRegisteredObject<MapEvent>(context.MapEventId).BattleState = BattleState.DefenderVictory;
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, new NetworkAdvanceBattleSimulation(context.MapEventId, 1));
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());
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

            if (menuConsequence)
                new TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior()
                    .game_menu_encounter_leave_on_consequence(null);
            else
                MenuHelper.EncounterLeaveConsequence();

            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.Party.MapEventSide);
        }, MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu))));
    }

}
