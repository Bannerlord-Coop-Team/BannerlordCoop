using Common.Messaging;
using Common.Util;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class SiegeCaptureTransitionRetryHandlerTests : IDisposable
{
    private readonly GameStateManager gameStateManager;
    private readonly MapState mapState;
    private readonly MapState inactiveMapState;

    public SiegeCaptureTransitionRetryHandlerTests()
    {
        GameBootStrap.Initialize();
        gameStateManager = Game.Current.GameStateManager;
        mapState = gameStateManager.CreateState<MapState>();
        gameStateManager._gameStates.Add(mapState);
        inactiveMapState = gameStateManager.CreateState<MapState>();
        gameStateManager._gameStates.Add(inactiveMapState);
    }

    public void Dispose()
    {
        gameStateManager._gameStates.Remove(inactiveMapState);
        gameStateManager._gameStates.Remove(mapState);
    }

    [Fact]
    public void CampaignTick_WaitsForSimulationPresentationThenRetriesPrompt()
    {
        using var messageBroker = new MessageBroker();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        using var handler = new SiegeCaptureTransitionRetryHandler(messageBroker, siegeEventInterface.Object);
        var leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var previousOwnerClan = ObjectHelper.SkipConstructor<Clan>();
        mapState._battleSimulation = ObjectHelper.SkipConstructor<BattleSimulation>();

        SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement, previousOwnerClan);
        messageBroker.Publish(this, new CampaignTick());

        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(It.IsAny<MobileParty>(), It.IsAny<Settlement>(), It.IsAny<Clan>()),
            Times.Never);

        mapState.EndBattleSimulation();
        messageBroker.Publish(this, new CampaignTick());

        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(leaderParty, settlement, previousOwnerClan),
            Times.Once);

        messageBroker.Publish(this, new CampaignTick());
        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(leaderParty, settlement, previousOwnerClan),
            Times.Once);
    }

    [Fact]
    public void VassalCapturePrompt_KeepsTheDefeatedOwnerAfterTheFiefMovesToTheRuler()
    {
        var defeatedClan = ObjectHelper.SkipConstructor<Clan>();
        var rulerClan = ObjectHelper.SkipConstructor<Clan>();
        var vassal = ObjectHelper.SkipConstructor<Hero>();
        vassal._clan = ObjectHelper.SkipConstructor<Clan>();
        var vassalCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        vassalCharacter._heroObject = vassal;
        var lordComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        lordComponent._leader = vassal;
        var leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
        leaderParty._partyComponent = lordComponent;
        // Vanilla SiegeCompleted hands a vassal's capture to the kingdom leader.
        var town = ObjectHelper.SkipConstructor<Town>();
        town._ownerClan = rulerClan;
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.Town = town;
        var previousPlayerTroop = Game.Current.PlayerTroop;
        Game.Current.PlayerTroop = vassalCharacter;
        // An open scoreboard parks the prompt in the retry instead of switching menus.
        mapState._battleSimulation = ObjectHelper.SkipConstructor<BattleSimulation>();
        var siegeEventInterface = new SiegeEventInterface();
        try
        {
            siegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement, defeatedClan);

            mapState.EndBattleSimulation();
            Assert.True(SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _, out var retriedOwnerClan));
            Assert.Same(defeatedClan, retriedOwnerClan);
        }
        finally
        {
            SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _, out _);
            siegeEventInterface.Dispose();
            Game.Current.PlayerTroop = previousPlayerTroop;
        }
    }
}
