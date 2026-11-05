using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.HeirSelection.Handlers;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Traits.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks the real heir-selection event handler before it caches inherited inventory.</summary>
public class HeirSelectionIssueCancellationTests
{
    private readonly ITestOutputHelper output;
    public HeirSelectionIssueCancellationTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void CharacterChangeEventCancelsTheQuestBeforeCopyingInheritedInventory()
    {
        using var quests = new StubIssueQuestGenerator();
        using var preconditions = new StubIssuePreconditions(QuestScenario.IssueType);
        using var environment = new E2ETestEnvironment(output);
        var scenario = new QuestScenario(environment);
        scenario.Accept(scenario.ClientA);
        var heirId = environment.CreateRegisteredObject<Hero>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        var inheritancePartyId = environment.CreateRegisteredObject<MobileParty>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var completed = new List<bool>();

        scenario.Server.Call(() =>
        {
            var server = scenario.Server;
            Assert.True(server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(heirId, out var heir));
            Assert.True(server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(inheritancePartyId, out var inheritanceParty));
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            InventoryQuest quest;
            using (new AllowedThread())
            {
                owner._heroState = Hero.CharacterStates.Dead;
                owner._partyBelongedTo = null;
                heir.Clan = clan;
                party.ItemRoster.AddToCounts(item, 2);
                quest = new InventoryQuest(giver, item);
                giver.Issue.IssueQuest = quest;
            }

            var players = server.Resolve<IPlayerManager>();
            Assert.True(server.ObjectManager.TryGetId(heir.CharacterObject, out var characterId));
            var replacement = new Player(QuestScenario.ControllerA, heirId, scenario.OwnerPartyId, clanId,
                characterId);
            var restorer = new Mock<IPlayerPartyRestorer>();
            restorer.Setup(value => value.TryRestore(It.IsAny<Player>(), out replacement)).Returns(true);
            var inheritance = new HeirSelectionCampaignBehaviorInterface(Mock.Of<ICoopSessionProvider>(),
                server.ObjectManager, players, Mock.Of<GameInterface.Services.Clans.IClanMemberGrouping>());
            var behavior = new Mock<IHeirSelectionCampaignBehaviorInterface>();
            behavior.Setup(value => value.OnBeforePlayerCharacterChanged(owner, party)).Callback(() =>
            {
                completed.Add(quest.IsFinalized && giver.Issue == null);
                inheritance.OnBeforePlayerCharacterChanged(owner, party);
            });
            var broker = new MessageBroker();
            using var handler = new HeirSelectionHandler(broker, server.ObjectManager, server.Resolve<INetwork>(),
                players, restorer.Object, Mock.Of<IApplyHeirSelectionActionInterface>(), behavior.Object,
                Mock.Of<ICoopSessionMigrator>(), Mock.Of<ITraitLevelingHelperInterface>(),
                server.Resolve<IIssueCancellationCoordinator>(), null, null);

            broker.Publish(this, new ChangePlayerCharacterAfterHeirSelection(owner, heir));

            Assert.True(Assert.Single(completed));
            Assert.True(quest.IsFinalized);
            Assert.Equal(1, quest.Cancellations);
            Assert.Same(owner, quest.CancellingHero);
            Assert.Same(party, quest.CancellingParty);
            inheritance.OnPlayerCharacterChanged(owner, heir, inheritanceParty, true);
            Assert.Equal(1, inheritanceParty.ItemRoster.GetItemNumber(item));
            Assert.Equal(heirId, Assert.Single(players.Players.Where(player => player.ControllerId == QuestScenario.ControllerA)).HeroId);
        });
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkChangePlayerCharacterAfterHeirSelection>());
    }

    /// <summary>Removes one inventory item as the native cancellation consequence.</summary>
    private sealed class InventoryQuest : UnstartedQuest
    {
        private readonly ItemObject item;
        public int Cancellations { get; private set; }
        public Hero CancellingHero { get; private set; } = null!;
        public MobileParty CancellingParty { get; private set; } = null!;
        public InventoryQuest(Hero giver, ItemObject item) : base("inheritance_quest", giver) => this.item = item;
        public override void OnCanceled()
        {
            Cancellations++;
            CancellingHero = Hero.MainHero;
            CancellingParty = MobileParty.MainParty;
            MobileParty.MainParty.ItemRoster.AddToCounts(item, -1);
        }
    }
}
