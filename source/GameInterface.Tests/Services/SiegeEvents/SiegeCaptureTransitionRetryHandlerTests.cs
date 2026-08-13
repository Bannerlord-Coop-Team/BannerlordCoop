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
        mapState._battleSimulation = ObjectHelper.SkipConstructor<BattleSimulation>();

        SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement);
        messageBroker.Publish(this, new CampaignTick());

        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(It.IsAny<MobileParty>(), It.IsAny<Settlement>()),
            Times.Never);

        mapState.EndBattleSimulation();
        messageBroker.Publish(this, new CampaignTick());

        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(leaderParty, settlement),
            Times.Once);

        messageBroker.Publish(this, new CampaignTick());
        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(leaderParty, settlement),
            Times.Once);
    }

}
