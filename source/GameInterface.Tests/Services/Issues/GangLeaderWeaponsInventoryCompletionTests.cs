using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class GangLeaderWeaponsInventoryCompletionTests
{
    static GangLeaderWeaponsInventoryCompletionTests() => RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OwnerCompletion_RunsActualCallbackOnlyForOwnedOngoingQuest(bool isTrading)
    {
        RunWithPlayer((hero, receiver) =>
        {
            var owned = AddQuest(7);
            var otherPlayer = AddQuest(9);
            var unowned = AddQuest(12);
            var finalized = AddQuest(11);
            finalized._questState = QuestBase.QuestStates.Finalized;
            var ownership = new Mock<IIssueOwnershipRegistry>();
            ownership.Setup(o => o.IsLocalPeerOwner(owned.QuestGiver)).Returns(true);
            ownership.Setup(o => o.IsLocalPeerOwner(finalized.QuestGiver)).Returns(true);
            var attemptedPurchases = new List<(ItemRosterElement, int)>
            {
                (new ItemRosterElement(new EquipmentElement(new ItemObject("inventory_callback_item")), 8), 100)
            };

            Deliver(hero, new NetworkUpdateTradeData("owner", attemptedPurchases, new List<(ItemRosterElement, int)>(), isTrading), ownership.Object);

            Assert.Equal(0, owned._collectedItemAmount);
            Assert.Equal(9, otherPlayer._collectedItemAmount);
            Assert.Equal(12, unowned._collectedItemAmount);
            Assert.Equal(11, finalized._collectedItemAmount);
            receiver.Verify(r => r.OnPlayerInventoryExchange(
                It.IsAny<List<(ItemRosterElement, int)>>(), It.IsAny<List<(ItemRosterElement, int)>>(), It.IsAny<bool>()), Times.Never);
        });
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ServerOtherPlayerOrUnknownHero_DoesNotRunQuestCallback(bool isServer, bool resolved, bool isOwner)
    {
        RunWithPlayer((hero, receiver) =>
        {
            var quest = AddQuest(7);
            var ownership = new Mock<IIssueOwnershipRegistry>();
            ownership.Setup(o => o.IsLocalPeerOwner(quest.QuestGiver)).Returns(true);
            ModInformation.IsServer = isServer;

            Deliver(isOwner ? hero : ObjectHelper.SkipConstructor<Hero>(),
                new NetworkUpdateTradeData("owner", null!, null!, false), ownership.Object, resolved);

            Assert.Equal(7, quest._collectedItemAmount);
            ownership.Verify(o => o.IsLocalPeerOwner(It.IsAny<Hero>()), Times.Never);
        });
    }

    private static Quest AddQuest(int previousCount)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest.QuestGiver = ObjectHelper.SkipConstructor<Hero>();
        quest._collectedItemAmount = previousCount;
        Campaign.Current.QuestManager.OnQuestStarted(quest);
        return quest;
    }

    private static void Deliver(Hero hero, NetworkUpdateTradeData message, IIssueOwnershipRegistry ownership, bool resolved = true)
    {
        using var broker = new MessageBroker();
        var objects = new Mock<IObjectManager>();
        objects.Setup(o => o.TryGetObjectWithLogging(message.PlayerHeroId, out hero)).Returns(resolved);
        using var handler = new GangLeaderWeaponsIssueHandler(broker, Mock.Of<INetwork>(), objects.Object,
            Mock.Of<IPlayerManager>(), Mock.Of<IIssueGenerationRegistry>(), ownership,
            null!, null!);
        broker.Publish(typeof(GangLeaderWeaponsInventoryCompletionTests), message);
    }

    private static void RunWithPlayer(Action<Hero, Mock<CampaignEventReceiver>> test)
    {
        GameThread.Run(() =>
        {
            var previousGame = Game.Current;
            var previousCampaign = Campaign.Current;
            bool wasServer = ModInformation.IsServer;
            try
            {
                ModInformation.IsServer = false;
                var hero = ObjectHelper.SkipConstructor<Hero>();
                var character = ObjectHelper.SkipConstructor<CharacterObject>();
                character._heroObject = hero;
                hero._characterObject = character;
                Game.Current = ObjectHelper.SkipConstructor<Game>();
                Game.Current.PlayerTroop = character;
                Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
                Campaign.Current.QuestManager = new QuestManager();
                var party = ObjectHelper.SkipConstructor<MobileParty>();
                party.Party = ObjectHelper.SkipConstructor<PartyBase>();
                party.Party.ItemRoster = new ItemRoster();
                Campaign.Current.MainParty = party;
                var receiver = new Mock<CampaignEventReceiver>();
                Campaign.Current.CampaignEventDispatcher = new CampaignEventDispatcher(new[] { receiver.Object });
                test(hero, receiver);
            }
            finally
            {
                ModInformation.IsServer = wasServer;
                Game.Current = previousGame;
                Campaign.Current = previousCampaign;
            }
        }, blocking: true);
    }
}
