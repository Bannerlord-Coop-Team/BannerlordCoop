using Common.Tests.Utils;
using Coop.Core.Client.Services.Heroes.Handlers;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Heroes.Handlers;

public class RemotePlayerHeroHandlerTests
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly Mock<IHeroInterface> heroInterface = new();
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly Mock<IControllerIdMigration> controllerIdMigration = new();
    private readonly Mock<IPlayerCreationRollback> playerCreationRollback = new();
    private readonly RemotePlayerHeroHandler handler;

    public RemotePlayerHeroHandlerTests()
    {
        handler = new RemotePlayerHeroHandler(
            messageBroker,
            heroInterface.Object,
            playerManager.Object,
            controllerIdMigration.Object,
            playerCreationRollback.Object);
    }

    [Fact]
    public void PlayerCreationRolledBack_RemovesExactRegistrationAndGraph()
    {
        var announcedPlayer = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        var registeredPlayer = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        var registrationIds = new[] { "Hero_hero1", "TroopRoster_party1" };
        playerManager
            .Setup(manager => manager.TryGetPlayer(announcedPlayer.ControllerId, out registeredPlayer))
            .Returns(true);

        messageBroker.Publish(this, new NetworkPlayerCreationRolledBack(announcedPlayer, registrationIds));

        playerManager.Verify(manager => manager.RemovePlayer(registeredPlayer), Times.Once);
        playerCreationRollback.Verify(
            rollback => rollback.Rollback(announcedPlayer, registrationIds),
            Times.Once);
    }

    private static NetworkNewPlayerHeroCreated NewHeroMessage(out Player player, out byte[] heroData)
    {
        player = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        heroData = new byte[] { 1, 2, 3 };
        return new NetworkNewPlayerHeroCreated("ctrl", player, heroData);
    }

    [Fact]
    public void NewPlayerHeroCreated_RegistersAndUnpacksImmediately()
    {
        var message = NewHeroMessage(out var player, out var heroData);
        playerManager.Setup(x => x.AddPlayer(player)).Returns(true);

        // No campaign-ready gate any more: the server queue already withheld this until the client is
        // in the campaign, so it is handled the instant it arrives.
        messageBroker.Publish(this, message);

        playerManager.Verify(x => x.AddPlayer(player), Times.Once);
        heroInterface.Verify(x => x.ClientUnpackHero(heroData, player), Times.Once);
    }

    [Fact]
    public void ExistingPlayer_NoHeroData_RegistersWithoutUnpacking()
    {
        // A player already in the session when we joined carries no hero blob (its hero is in the save we
        // loaded), so it is registered as controlled without unpacking.
        var player = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        playerManager.Setup(x => x.AddPlayer(player)).Returns(true);

        messageBroker.Publish(this, new NetworkNewPlayerHeroCreated("ctrl", player, System.Array.Empty<byte>()));

        playerManager.Verify(x => x.AddPlayer(player), Times.Once);
        heroInterface.Verify(x => x.ClientUnpackHero(It.IsAny<byte[]>(), It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public void DuplicatePlayer_DoesNotUnpack()
    {
        var message = NewHeroMessage(out var player, out _);
        // Already-known player: TryGetPlayer reports it, so the handler must bail before unpacking.
        playerManager
            .Setup(x => x.TryGetPlayer(player.ControllerId, out It.Ref<Player>.IsAny))
            .Returns(true);

        messageBroker.Publish(this, message);

        // A duplicate registration is logged and skipped — never unpacked or re-registered.
        heroInterface.Verify(x => x.ClientUnpackHero(It.IsAny<byte[]>(), It.IsAny<Player>()), Times.Never);
        playerManager.Verify(x => x.AddPlayer(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public void PlayerRegistrationUpdated_ReplacesExistingMapping()
    {
        var registered = new Player("ctrl", "hero1", "staleParty", "clan1", "char1");
        var replacement = new Player("ctrl", "hero1", "party1", "clan1", "char1");
        var resolvedPlayer = registered;
        playerManager
            .Setup(manager => manager.TryGetPlayer(registered.ControllerId, out resolvedPlayer))
            .Returns(true);
        playerManager.Setup(manager => manager.ReplacePlayer(registered, replacement)).Returns(true);

        messageBroker.Publish(this, new NetworkPlayerRegistrationUpdated(replacement));

        playerManager.Verify(manager => manager.ReplacePlayer(registered, replacement), Times.Once);
    }

    [Fact]
    public void PlayerRegistrationUpdated_ChangedControllerId_MigratesExistingMapping()
    {
        const string legacyControllerId = "123456789";
        var replacement = new Player(
            "gog:123456789",
            "hero1",
            "party1",
            "clan1",
            "char1");
        var migratedPlayer = replacement;
        controllerIdMigration
            .Setup(migration => migration.TryMigrate(
                legacyControllerId,
                replacement.ControllerId,
                out migratedPlayer))
            .Returns(true);

        messageBroker.Publish(
            this,
            new NetworkPlayerRegistrationUpdated(replacement, legacyControllerId));

        controllerIdMigration.Verify(migration => migration.TryMigrate(
            legacyControllerId,
            replacement.ControllerId,
            out migratedPlayer), Times.Once);
        playerManager.Verify(
            manager => manager.ReplacePlayer(It.IsAny<Player>(), It.IsAny<Player>()),
            Times.Never);
    }
}
