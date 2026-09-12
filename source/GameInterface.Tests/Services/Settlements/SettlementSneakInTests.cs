using Common;
using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Handlers;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Handlers;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

[Collection(ModInformationRoleCollection.Name)]
public class SettlementSneakInTests
{
    public SettlementSneakInTests()
    {
        // Needed for test environment to have Campaign models
        GameBootStrap.Initialize();

        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void SettlementSneakInPlayerCaptured_DoesntRemoveCompanionsAndTroops()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerHero._heroState = Hero.CharacterStates.Active;
        playerCharacter.HeroObject = playerHero;
        playerHero._characterObject = playerCharacter;

        var companion = ObjectHelper.SkipConstructor<Hero>();
        var companionCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        companion._heroState = Hero.CharacterStates.Active;
        companionCharacter.HeroObject = companion;
        companion._characterObject = companionCharacter;

        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        var vanillaPlayerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();

        var playerParty = ObjectHelper.SkipConstructor<MobileParty>();
        var playerPartyBase = ObjectHelper.SkipConstructor<PartyBase>();
        playerParty.Party = playerPartyBase;
        playerParty.IsActive = true;
        playerPartyBase.MobileParty = playerParty;
        playerPartyBase.MemberRoster = new TroopRoster(playerPartyBase);
        playerPartyBase.PrisonRoster = new TroopRoster(playerPartyBase);
        playerPartyBase.MemberRoster.AddToCounts(playerCharacter, 1);
        playerPartyBase.MemberRoster.AddToCounts(companionCharacter, 1);
        playerPartyBase.MemberRoster.AddToCounts(troop, 10);

        var playerObjects = (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

        var settlement = ObjectHelper.SkipConstructor<Settlement>();

        var settlementParty = ObjectHelper.SkipConstructor<PartyBase>();
        settlement.Party = settlementParty;
        settlementParty.PrisonRoster = new TroopRoster(settlementParty);

        var messageBroker = MessageBroker.Instance;
        var objectManager = new Mock<IObjectManager>();
        var network = new Mock<INetwork>();
        var sessionInteractionsInterface = new Mock<ISessionInteractionsPlayerDataInterface>();
        var playerManager = new Mock<IPlayerManager>();

        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(policy => policy.AllowOriginal()).Returns(false);
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        using var container = containerBuilder.Build();

        var harmony = new Harmony(nameof(SettlementSneakInPlayerCaptured_DoesntRemoveCompanionsAndTroops));
        bool wasServer = ModInformation.IsServer;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        var eventDispatcher = CampaignEventDispatcher.Instance;
        var previousEventReceivers = eventDispatcher._eventReceivers;
        var previousPlayerTroop = Game.Current.PlayerTroop;

        SetupObject(objectManager, "hero-1", playerHero);
        SetupObject(objectManager, "settlement-1", settlement);
        var message = new NetworkTakenPrisonerDuringSneakIn("hero-1", "settlement-1");

        try
        {
            playerObjects.Add(playerParty, new ControlledObjectInfo("TestPlayer", null!));
            // Vanilla reads both globals while TakePrisonerAction completes.
            eventDispatcher._eventReceivers = Array.Empty<CampaignEventReceiver>();
            Game.Current.PlayerTroop = vanillaPlayerCharacter;
            harmony.CreateClassProcessor(typeof(TakePrisonerActionPatches)).Patch();
            ModInformation.IsServer = true;

            Assert.True(playerParty.IsPlayerParty());
            Assert.Same(playerParty, playerHero.PartyBelongedTo);

            using var sneakInHandler = new SettlementSneakInHandler(
                messageBroker,
                objectManager.Object,
                network.Object,
                sessionInteractionsInterface.Object);

            using var captivityHandler = new PlayerCaptivityServerHandler(
                objectManager.Object,
                network.Object,
                messageBroker,
                playerManager.Object);

            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                messageBroker.Publish(this, message);
                GameThread.Run(() => { }, blocking: true);
            }

            Assert.True(playerParty.IsActive);
            Assert.Equal(11, playerPartyBase.MemberRoster.TotalManCount);
            Assert.False(playerPartyBase.MemberRoster.Contains(playerCharacter));
            Assert.Equal(1, playerPartyBase.MemberRoster.GetElementNumber(companionCharacter));
            Assert.Equal(10, playerPartyBase.MemberRoster.GetElementNumber(troop));
            Assert.Equal(1, settlementParty.PrisonRoster.TotalManCount);
            Assert.True(settlementParty.PrisonRoster.Contains(playerCharacter));
            Assert.Same(settlementParty, playerHero.PartyBelongedToAsPrisoner);
            Assert.Null(playerHero.PartyBelongedTo);
            Assert.Equal(Hero.CharacterStates.Prisoner, playerHero.HeroState);
            Assert.Same(playerParty, companion.PartyBelongedTo);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            playerObjects.Remove(playerParty);
            eventDispatcher._eventReceivers = previousEventReceivers;
            Game.Current.PlayerTroop = previousPlayerTroop;
            ModInformation.IsServer = wasServer;

            if (hadContainer)
                ContainerProvider.SetContainer(previousContainer);
            else
                ContainerProvider.Clear();
        }
    }

    private static void SetupObject<T>(Mock<IObjectManager> objectManager, string id, T instance)
        where T : class
    {
        objectManager.Setup(manager => manager.TryGetObjectWithLogging(id, out instance)).Returns(true);
    }
}
