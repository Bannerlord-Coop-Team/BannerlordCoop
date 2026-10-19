using Common.Messaging;
using Common.Util;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Verifies the parked capture transition waits for the battle to resolve and for a still-held choice.</summary>
[Collection(nameof(CampaignCurrentCollection))]
public sealed class SiegeCaptureTransitionRetryHandlerTests : IDisposable
{
    private readonly GameStateManager gameStateManager;
    private readonly MapState mapState;
    private readonly MapState inactiveMapState;
    private readonly MessageBroker messageBroker = new MessageBroker();
    private readonly Mock<ISiegeEventInterface> siegeEventInterface = new Mock<ISiegeEventInterface>();
    private readonly SiegeCaptureTransitionRetryHandler handler;
    private readonly MobileParty leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();

    public SiegeCaptureTransitionRetryHandlerTests()
    {
        GameBootStrap.Initialize();
        gameStateManager = Game.Current.GameStateManager;
        mapState = gameStateManager.CreateState<MapState>();
        gameStateManager._gameStates.Add(mapState);
        inactiveMapState = gameStateManager.CreateState<MapState>();
        gameStateManager._gameStates.Add(inactiveMapState);
        handler = new SiegeCaptureTransitionRetryHandler(messageBroker, siegeEventInterface.Object);
    }

    public void Dispose()
    {
        handler.Dispose();
        messageBroker.Dispose();
        SiegeCaptureMenuHoldPatch.Release(settlement);
        Campaign.Current.PlayerEncounter = null;
        gameStateManager._gameStates.Remove(inactiveMapState);
        gameStateManager._gameStates.Remove(mapState);
    }

    [Fact]
    public void CampaignTick_WaitsForSimulationPresentationThenRetriesPrompt()
    {
        mapState._battleSimulation = ObjectHelper.SkipConstructor<BattleSimulation>();
        SiegeCaptureMenuHoldPatch.HoldFor(settlement);

        SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement);
        messageBroker.Publish(this, new CampaignTick());
        VerifyPrompted(Times.Never());

        mapState.EndBattleSimulation();
        messageBroker.Publish(this, new CampaignTick());
        VerifyPrompted(Times.Once());

        messageBroker.Publish(this, new CampaignTick());
        VerifyPrompted(Times.Once());
    }

    [Fact]
    public void CampaignTick_WaitsForThePostBattleLootSteps()
    {
        // the loot screens still run off the live encounter; finishing it first would drop the loot
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter.EncounterState = PlayerEncounterState.LootParty;
        Campaign.Current.PlayerEncounter = encounter;
        SiegeCaptureMenuHoldPatch.HoldFor(settlement);

        SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement);
        messageBroker.Publish(this, new CampaignTick());
        VerifyPrompted(Times.Never());

        Campaign.Current.PlayerEncounter = null;
        messageBroker.Publish(this, new CampaignTick());
        VerifyPrompted(Times.Once());
    }

    [Fact]
    public void CampaignTick_ReleasedChoice_DropsTheRetry()
    {
        // the encounter End opened the menu and the player picked before the retry got its tick
        SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement);
        messageBroker.Publish(this, new CampaignTick());

        VerifyPrompted(Times.Never());
        Assert.False(SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _));
    }

    [Fact]
    public void VassalCapturePrompt_HoldsTheDefeatedOwnerAfterTheFiefMovesToTheRuler()
    {
        var defeatedClan = ObjectHelper.SkipConstructor<Clan>();
        var vassal = ObjectHelper.SkipConstructor<Hero>();
        vassal._clan = ObjectHelper.SkipConstructor<Clan>();
        var vassalCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        vassalCharacter._heroObject = vassal;
        var lordComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        lordComponent._leader = vassal;
        leaderParty._partyComponent = lordComponent;
        // Vanilla SiegeCompleted hands a vassal's capture to the kingdom leader.
        var town = ObjectHelper.SkipConstructor<Town>();
        town._ownerClan = ObjectHelper.SkipConstructor<Clan>();
        settlement.Town = town;
        var previousPlayerTroop = Game.Current.PlayerTroop;
        Game.Current.PlayerTroop = vassalCharacter;
        // An open scoreboard parks the prompt in the retry instead of switching menus.
        mapState._battleSimulation = ObjectHelper.SkipConstructor<BattleSimulation>();
        var realSiegeEventInterface = new SiegeEventInterface();
        try
        {
            realSiegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement, defeatedClan);

            Assert.Same(defeatedClan, SiegeCaptureMenuHoldPatch.GetHeldPreviousOwner(settlement));
            mapState.EndBattleSimulation();
            Assert.True(SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out var retriedSettlement));
            Assert.Same(settlement, retriedSettlement);
        }
        finally
        {
            SiegeCaptureTransitionRetryHandler.TryTakeReady(out _, out _);
            realSiegeEventInterface.Dispose();
            Game.Current.PlayerTroop = previousPlayerTroop;
        }
    }

    private void VerifyPrompted(Times times)
    {
        siegeEventInterface.Verify(
            service => service.PromptLocalAftermathChoice(leaderParty, settlement, null),
            times);
    }
}
