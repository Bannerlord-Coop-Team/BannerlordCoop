using Common.Network;
using Common.Tests.Utils;
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
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class SaveGameHandlerTests
{
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
            network.Object);

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
    public void AllGameObjectsRegistered_RepairsSavedPlayerBeforeRegistration()
    {
        var messageBroker = new TestMessageBroker();
        var sessionProvider = new Mock<ICoopSessionProvider>();
        var playerRegistry = new Mock<IPlayerManager>();
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var player = new Player("controller", "Hero_Player1", "MobileParty_Player1", "Clan_Player1", "CharacterObject_Player1");
        var empty = CoopSession.Empty;
        var loadedSession = new CoopSession(
            "save",
            new[] { player },
            empty.CraftingPlayerData,
            empty.WorkshopPlayerData,
            empty.CaravansPlayerData,
            empty.AlleyPlayerData,
            empty.InteractionsPlayerData,
            empty.TradePlayerData,
            empty.InventoryPlayerData);
        var saveManager = new TestCoopSaveManager(loadedSession);

        var calls = new List<string>();
        playerPartyRestorer
            .Setup(value => value.Restore(player))
            .Callback(() => calls.Add("repair"));
        playerRegistry
            .Setup(value => value.AddPlayer(player))
            .Callback(() => calls.Add("register"))
            .Returns(true);

        using var handler = new SaveGameHandler(
            messageBroker,
            saveManager,
            sessionProvider.Object,
            playerRegistry.Object,
            playerPartyRestorer.Object,
            Mock.Of<INetwork>());

        messageBroker.Publish(this, new GameLoaded("save"));
        messageBroker.Publish(this, new AllGameObjectsRegistered());

        Assert.Equal(new[] { "repair", "register" }, calls);
    }

    private sealed class TestCoopSaveManager : ICoopSaveManager
    {
        private readonly ICoopSession loadedSession;

        public string DefaultPath => string.Empty;
        public string FileType => string.Empty;

        public TestCoopSaveManager(ICoopSession loadedSession)
        {
            this.loadedSession = loadedSession;
        }

        public void SaveCoopSession(string saveName, ICoopSession session)
        {
        }

        public ICoopSession LoadCoopSession(string saveName) => loadedSession;
    }
}
