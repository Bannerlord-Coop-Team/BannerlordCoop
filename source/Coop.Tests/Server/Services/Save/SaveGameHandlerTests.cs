using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Save.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const string StalePartyId = "Party_Stale";
    private const string DeadHeroId = "Hero_Dead";
    private const string DeadPartyId = "Party_Dead";
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

    [Fact]
    public void GameSaved_WritesSessionAndReportsWritten()
    {
        var messageBroker = new TestMessageBroker();
        var saveManager = new StubSaveManager(null!);
        using var handler = CreateSavingHandler(messageBroker, saveManager);

        messageBroker.Publish(new object(), new GameSaved(SaveName));

        Assert.Equal(new[] { SaveName }, saveManager.WrittenSaveNames);
        var written = Assert.Single(messageBroker.Messages.GetMessages<CoopSessionWritten>());
        Assert.Equal(SaveName, written.SaveName);
        Assert.True(written.Success);
    }

    [Fact]
    public void GameSaved_SessionWriteThrows_ReportsNotWritten()
    {
        var messageBroker = new TestMessageBroker();
        var saveManager = new StubSaveManager(null!, new IOException("disk full"));
        using var handler = CreateSavingHandler(messageBroker, saveManager);

        // TestMessageBroker does not catch subscriber exceptions, so a leaked throw fails here.
        messageBroker.Publish(new object(), new GameSaved(SaveName));

        var written = Assert.Single(messageBroker.Messages.GetMessages<CoopSessionWritten>());
        Assert.Equal(SaveName, written.SaveName);
        Assert.False(written.Success);
    }

    [Fact]
    public void GameSaved_SessionSnapshotThrows_ReportsNotWritten()
    {
        var messageBroker = new TestMessageBroker();
        var saveManager = new StubSaveManager(null!);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.SetupGet(registry => registry.Players).Throws(new InvalidOperationException("registry changed"));
        using var handler = CreateSavingHandler(messageBroker, saveManager, playerRegistry.Object);

        messageBroker.Publish(new object(), new GameSaved(SaveName));

        Assert.Empty(saveManager.WrittenSaveNames);
        var written = Assert.Single(messageBroker.Messages.GetMessages<CoopSessionWritten>());
        Assert.Equal(SaveName, written.SaveName);
        Assert.False(written.Success);
    }

    [Fact]
    public void AllGameObjectsRegistered_DeadHeroNamesAnotherPlayersParty_RestoresThePartyOwner()
    {
        var owner = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        // A dead hero's row keeps its saved party through the restore, so it can name a party another player's hero owns
        var deadClaim = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var saved = new[] { deadClaim, owner };
        var playerPartyRestorer = RestoresEachRow(saved);
        KeepsItsParty(playerPartyRestorer, owner);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, saved, playerPartyRestorer);

        playerRegistry.Verify(registry => registry.AddPlayer(owner), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_OwnerRestoreFails_DeadHeroStillDoesNotGetTheParty()
    {
        var liveHero = ObjectHelper.SkipConstructor<Hero>();
        var owner = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var deadClaim = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = new[] { deadClaim, owner, unrelated };
        var playerPartyRestorer = RestoresEachRow(deadClaim, unrelated);
        // The real restorer hands back the row it was given even when it fails
        var restoredOwner = owner;
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(owner, out restoredOwner)).Returns(false);
        KeepsItsParty(playerPartyRestorer, unrelated);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        playerPartyRestorer
            .Setup(restorer => restorer.TryGetOwnedParty(
                deadClaim, It.Is<IReadOnlyCollection<Hero>>(heroes => !heroes.Contains(liveHero)), out party))
            .Returns(true);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        // The dead hero's party is checked against every saved hero, not only the restored ones
        using var handler = CreateHandler(playerRegistry, saved, playerPartyRestorer, playerHeroes: new[] { liveHero });

        playerRegistry.Verify(registry => registry.AddPlayer(deadClaim), Times.Never);
        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_DistinctControllers_SkipsUnrestoredRegistration()
    {
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_A");
        var second = new Player("PlayerTwo", MissingHeroId, "Party_B", "Clan_Two", "Character_B");

        var playerPartyRestorer = RestoresEachRow(first);
        KeepsItsParty(playerPartyRestorer, first);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second }, playerPartyRestorer);

        playerRegistry.Verify(registry => registry.AddPlayer(first), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(second), Times.Never);
    }

    [Fact]
    public void AllGameObjectsRegistered_RestorerThrowsForOneRow_RegistersTheOthers()
    {
        // The restorer calls into vanilla party code, which can throw on one bad row
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_A");
        var broken = new Player("PlayerTwo", StaleHeroId, StalePartyId, "Clan_Two", "Character_B");
        var third = new Player("PlayerThree", DeadHeroId, DeadPartyId, "Clan_Three", "Character_C");
        var playerPartyRestorer = RestoresEachRow(first, third);
        playerPartyRestorer
            .Setup(restorer => restorer.TryRestore(broken, out It.Ref<Player>.IsAny))
            .Throws(new InvalidOperationException("party roster broken"));
        KeepsItsParty(playerPartyRestorer, first);
        KeepsItsParty(playerPartyRestorer, third);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        // TestMessageBroker does not catch subscriber exceptions, so a leaked throw fails here
        using var handler = CreateHandler(playerRegistry, new[] { first, broken, third }, playerPartyRestorer);

        playerRegistry.Verify(registry => registry.AddPlayer(first), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(third), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Exactly(2));
    }

    [Fact]
    public void AllGameObjectsRegistered_PublishesRestoredAfterEveryRegistration()
    {
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_A");
        var second = new Player("PlayerTwo", StaleHeroId, StalePartyId, "Clan_Two", "Character_B");
        var order = new List<string>();

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry
            .Setup(registry => registry.AddPlayer(It.IsAny<Player>()))
            .Callback(() => order.Add("registered"))
            .Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            new[] { first, second },
            configureBroker: broker => broker.Subscribe<SavedPlayerRegistrationsRestored>(
                _ => order.Add("restored")));

        Assert.Equal(new[] { "registered", "registered", "restored" }, order);
    }

    [Fact]
    public void AllGameObjectsRegistered_RepairsSavedPlayerBeforeRegistration()
    {
        var player = new Player(ControllerId, LiveHeroId, "Party_Gone", "Clan_One", "Character_Live");
        var repaired = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var calls = new List<string>();
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var playerRegistry = new Mock<IPlayerManager>();
        var restoredPlayer = repaired;

        playerPartyRestorer
            .Setup(restorer => restorer.TryRestore(player, out restoredPlayer))
            .Callback(() => calls.Add("repair"))
            .Returns(true);
        KeepsItsParty(playerPartyRestorer, repaired);
        playerRegistry
            .Setup(registry => registry.AddPlayer(repaired))
            .Callback(() => calls.Add("register"))
            .Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { player }, playerPartyRestorer);

        Assert.Equal(new[] { "repair", "register" }, calls);
    }

    // Drives a handler through GameLoaded, then AllGameObjectsRegistered, with the selector picking every row. Without
    // a given restorer, every row is restored as it is and keeps its party.
    private static SaveGameHandler CreateHandler(
        Mock<IPlayerManager> playerRegistry,
        Player[] rows,
        Mock<IPlayerPartyRestorer> playerPartyRestorer = null,
        Action<TestMessageBroker>? configureBroker = null,
        IReadOnlyCollection<Hero> playerHeroes = null)
    {
        var messageBroker = new TestMessageBroker();
        if (playerPartyRestorer == null)
        {
            playerPartyRestorer = RestoresEachRow(rows);
            foreach (var player in rows)
                KeepsItsParty(playerPartyRestorer, player);
        }
        configureBroker?.Invoke(messageBroker);

        var heroes = playerHeroes ?? Array.Empty<Hero>();
        var registrationSelector = new Mock<ISavedPlayerRegistrationSelector>();
        registrationSelector.Setup(selector => selector.Select(It.IsAny<IEnumerable<Player>>(), out heroes)).Returns(rows);

        var handler = new SaveGameHandler(
            messageBroker,
            new StubSaveManager(SessionWith(rows)),
            new Mock<ICoopSessionProvider>().Object,
            playerRegistry.Object,
            playerPartyRestorer.Object,
            new Mock<INetwork>().Object,
            registrationSelector.Object);

        messageBroker.Publish(new object(), new GameLoaded(SaveName));
        messageBroker.Publish(new object(), new AllGameObjectsRegistered());

        return handler;
    }

    private static Mock<IPlayerPartyRestorer> RestoresEachRow(params Player[] rows)
    {
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        foreach (var row in rows)
        {
            var restoredPlayer = row;
            playerPartyRestorer.Setup(restorer => restorer.TryRestore(row, out restoredPlayer)).Returns(true);
        }

        return playerPartyRestorer;
    }

    // The campaign still gives the row a party of its own
    private static void KeepsItsParty(Mock<IPlayerPartyRestorer> playerPartyRestorer, Player player)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        playerPartyRestorer
            .Setup(restorer => restorer.TryGetOwnedParty(player, It.IsAny<IReadOnlyCollection<Hero>>(), out party))
            .Returns(true);
    }

    private static SaveGameHandler CreateSavingHandler(
        TestMessageBroker messageBroker,
        StubSaveManager saveManager,
        IPlayerManager? playerRegistry = null)
    {
        if (playerRegistry == null)
        {
            var registry = new Mock<IPlayerManager>();
            registry.SetupGet(value => value.Players).Returns(Array.Empty<Player>());
            playerRegistry = registry.Object;
        }

        return new SaveGameHandler(
            messageBroker,
            saveManager,
            new Mock<ICoopSessionProvider>().Object,
            playerRegistry,
            new Mock<IPlayerPartyRestorer>().Object,
            new Mock<INetwork>().Object,
            null!);
    }

    /// <summary>
    /// Serves one loaded session. A hand-written stub rather than a mock because ICoopSaveManager
    /// is internal to Coop.Core, which is not marked visible to Moq's proxy generator.
    /// </summary>
    private sealed class StubSaveManager : ICoopSaveManager
    {
        private readonly ICoopSession session;
        private readonly Exception? writeFailure;

        public StubSaveManager(ICoopSession session, Exception? writeFailure = null)
        {
            this.session = session;
            this.writeFailure = writeFailure;
        }

        public string DefaultPath => string.Empty;
        public string FileType => ".json";
        public List<string> WrittenSaveNames { get; } = new List<string>();

        public ICoopSession LoadCoopSession(string saveName) => session;

        public void SaveCoopSession(string saveName, ICoopSession session)
        {
            if (writeFailure != null) throw writeFailure;

            WrittenSaveNames.Add(saveName);
        }
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
            empty.InventoryPlayerData,
            empty.HeroMeetingData,
            empty.AgingPlayerData,
            empty.NavalPlayerData);
    }
}
