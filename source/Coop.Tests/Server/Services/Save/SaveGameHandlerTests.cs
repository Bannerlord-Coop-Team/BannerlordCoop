using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Save.Messages;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class SaveGameHandlerTests
{
    private const string SaveName = "TestSave";
    private const string ControllerId = "PlayerOne";
    private const string LiveHeroId = "Hero_Live";
    private const string LivePartyId = "Party_Live";
    private const string StaleHeroId = "Hero_Stale";
    private const string MissingHeroId = "Hero_Missing";

    [Fact]
    public void GameSaveStateChanged_BroadcastsFirstStartAndLastEnd()
    {
        var messageBroker = new TestMessageBroker();
        var network = new Mock<INetwork>();
        using var handler = new SaveGameHandler(
            messageBroker,
            null!,
            null!,
            null!,
            null!,
            network.Object,
            null!);

        var firstSave = new object();
        var secondSave = new object();

        messageBroker.Publish(firstSave, new GameSaveStateChanged(true));
        messageBroker.Publish(secondSave, new GameSaveStateChanged(true));
        messageBroker.Publish(firstSave, new GameSaveStateChanged(false));
        messageBroker.Publish(secondSave, new GameSaveStateChanged(false));

        network.Verify(
            value => value.SendAll(It.Is<NetworkGameSaveStateChanged>(
                message => message.IsSaving)),
            Times.Once);
        network.Verify(
            value => value.SendAll(It.Is<NetworkGameSaveStateChanged>(
                message => !message.IsSaving)),
            Times.Once);
        network.VerifyNoOtherCalls();
    }

    [Theory]
    // A save written before controller ids were unique can hold the duplicates in either order,
    // and the dead one comes first as often as not.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DuplicateController_RestoresTheRegistrationWhoseGraphExists(
        bool missingFirst)
    {
        var missing = new Player(ControllerId, MissingHeroId, "Party_Missing", "Clan_One", "Character_Missing");
        var live = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var saved = missingFirst ? new[] { missing, live } : new[] { live, missing };

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, saved);

        // Restoring the registration that comes first would keep the dead one half the time,
        // leaving the live hero unregistered and sending its owner to build another character.
        playerRegistry.Verify(registry => registry.AddPlayer(live), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(missing), Times.Never);
    }

    [Fact]
    public void AllGameObjectsRegistered_DuplicateController_DropsRegistrationWithMissingParty()
    {
        var stale = new Player(ControllerId, StaleHeroId, "Party_Stale", "Clan_One", "Character_Stale");
        var live = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { stale, live });

        playerRegistry.Verify(registry => registry.AddPlayer(live), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(stale), Times.Never);
    }

    [Fact]
    public void AllGameObjectsRegistered_DuplicateControllerWithNoLiveHero_RestoresExactlyOne()
    {
        var first = new Player(ControllerId, MissingHeroId, "Party_A", "Clan_One", "Character_A");
        var second = new Player(ControllerId, "Hero_AlsoMissing", "Party_B", "Clan_One", "Character_B");

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second });

        // Neither hero resolves, so there is nothing to prefer — but the controller must still end
        // up with one registration, which its owner heals by joining.
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(first), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_DistinctControllers_RestoresEvery()
    {
        var first = new Player("PlayerOne", LiveHeroId, "Party_A", "Clan_One", "Character_A");
        var second = new Player("PlayerTwo", MissingHeroId, "Party_B", "Clan_Two", "Character_B");

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second });

        // A missing hero is only a tie-breaker between duplicates; on its own it must not cost a
        // controller its registration, or that player is sent to character creation for nothing.
        playerRegistry.Verify(registry => registry.AddPlayer(first), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(second), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_RepairsSavedPlayerBeforeRegistration()
    {
        var player = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var calls = new List<string>();
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var playerRegistry = new Mock<IPlayerManager>();

        playerPartyRestorer
            .Setup(restorer => restorer.Restore(player))
            .Callback(() => calls.Add("repair"));
        playerRegistry
            .Setup(registry => registry.AddPlayer(player))
            .Callback(() => calls.Add("register"))
            .Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { player }, playerPartyRestorer);

        Assert.Equal(new[] { "repair", "register" }, calls);
    }

    /// <summary>
    /// Builds a handler over a loaded session holding <paramref name="savedPlayers"/> and drives it
    /// through the load sequence (GameLoaded, then AllGameObjectsRegistered). Only
    /// <see cref="LiveHeroId"/> resolves in the object manager.
    /// </summary>
    private static SaveGameHandler CreateHandler(
        Mock<IPlayerManager> playerRegistry,
        Player[] savedPlayers,
        Mock<IPlayerPartyRestorer> playerPartyRestorer = null)
    {
        var messageBroker = new TestMessageBroker();
        if (playerPartyRestorer == null)
            playerPartyRestorer = new Mock<IPlayerPartyRestorer>();

        var objectManager = new Mock<IObjectManager>();
        Hero liveHero = null!;
        MobileParty liveParty = null!;
        Hero staleHero = null!;
        objectManager.Setup(manager => manager.TryGetObject(LiveHeroId, out liveHero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LivePartyId, out liveParty)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(StaleHeroId, out staleHero)).Returns(true);

        var handler = new SaveGameHandler(
            messageBroker,
            new StubSaveManager(SessionWith(savedPlayers)),
            new Mock<ICoopSessionProvider>().Object,
            playerRegistry.Object,
            playerPartyRestorer.Object,
            new Mock<INetwork>().Object,
            objectManager.Object);

        messageBroker.Publish(new object(), new GameLoaded(SaveName));
        messageBroker.Publish(new object(), new AllGameObjectsRegistered());

        return handler;
    }

    /// <summary>
    /// Serves one loaded session. A hand-written stub rather than a mock because ICoopSaveManager
    /// is internal to Coop.Core, which is not marked visible to Moq's proxy generator.
    /// </summary>
    private sealed class StubSaveManager : ICoopSaveManager
    {
        private readonly ICoopSession session;

        public StubSaveManager(ICoopSession session) => this.session = session;

        public string DefaultPath => string.Empty;
        public string FileType => ".json";

        public ICoopSession LoadCoopSession(string saveName) => session;

        public void SaveCoopSession(string saveName, ICoopSession session) { }
    }

    private static CoopSession SessionWith(Player[] players)
    {
        var empty = CoopSession.Empty;

        return new CoopSession(
            SaveName,
            players,
            empty.CraftingPlayerData,
            empty.WorkshopPlayerData,
            empty.CaravansPlayerData,
            empty.AlleyPlayerData,
            empty.InteractionsPlayerData,
            empty.TradePlayerData,
            empty.InventoryPlayerData);
    }
}
