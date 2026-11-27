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
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Save.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class SaveGameHandlerTests
{
    private const string SaveName = "TestSave";
    private const string ControllerId = "PlayerOne";
    private const string LiveHeroId = "Hero_Live";
    private const string LivePartyId = "Party_Live";
    // The object manager resolves an id with or without its type prefix, so both name the same party or hero
    private const string LivePartyFullId = "MobileParty_" + LivePartyId;
    private const string LiveHeroFullId = "Hero_" + LiveHeroId;
    private const string StaleHeroId = "Hero_Stale";
    private const string StalePartyId = "Party_Stale";
    private const string DeadHeroId = "Hero_Dead";
    private const string DeadPartyId = "Party_Dead";
    private const string DeadHeroFullId = "Hero_" + DeadHeroId;
    private const string DeadPartyFullId = "MobileParty_" + DeadPartyId;
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
        var stale = new Player(ControllerId, StaleHeroId, "Party_Gone", "Clan_One", "Character_Stale");
        var live = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { stale, live });

        playerRegistry.Verify(registry => registry.AddPlayer(live), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(stale), Times.Never);
    }

    [Fact]
    public void AllGameObjectsRegistered_DuplicateControllerWithNoLiveHero_RegistersNeither()
    {
        var first = new Player(ControllerId, MissingHeroId, "Party_A", "Clan_One", "Character_A");
        var second = new Player(ControllerId, "Hero_AlsoMissing", "Party_B", "Clan_One", "Character_B");

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second });

        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public void AllGameObjectsRegistered_TwoControllersClaimOneHero_RestoresNeither()
    {
        // The file can't tell which controller really owns the hero, so neither gets it.
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var second = new Player("PlayerTwo", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var other = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var restoredOther = other;
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(other, out restoredOther)).Returns(true);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second, other }, playerPartyRestorer);

        playerPartyRestorer.Verify(restorer => restorer.TryRestore(first, out It.Ref<Player>.IsAny), Times.Never);
        playerPartyRestorer.Verify(restorer => restorer.TryRestore(second, out It.Ref<Player>.IsAny), Times.Never);
        playerRegistry.Verify(registry => registry.AddPlayer(other), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // Both of PlayerOne's rows have a live graph, so picking one row per controller first would let
    // the row order decide whether the shared hero knocks out PlayerOne's other hero too.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_SharedHeroInDuplicateController_KeepsTheControllersOtherHero(
        bool sharedFirst)
    {
        var own = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var shared = new Player("PlayerOne", StaleHeroId, LivePartyId, "Clan_One", "Character_Stale");
        var claimed = new Player("PlayerTwo", StaleHeroId, StalePartyId, "Clan_Two", "Character_Stale");
        var saved = sharedFirst ? new[] { shared, own, claimed } : new[] { own, shared, claimed };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, saved);

        playerRegistry.Verify(registry => registry.AddPlayer(own), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // The same, but PlayerOne's dropped row names the hero by its full id and PlayerTwo by its short one,
    // so the id strings don't match and the dropped row would hide that two controllers claim it
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_SharedHeroByItsOtherIdInDuplicateController_KeepsTheControllersOtherHero(
        bool sharedFirst)
    {
        var own = new Player("PlayerOne", StaleHeroId, StalePartyId, "Clan_One", "Character_Stale");
        var shared = new Player("PlayerOne", LiveHeroFullId, LivePartyId, "Clan_One", "Character_Live");
        var claimed = new Player("PlayerTwo", LiveHeroId, LivePartyId, "Clan_Two", "Character_Live");
        var saved = sharedFirst ? new[] { shared, own, claimed } : new[] { own, shared, claimed };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, saved);

        playerRegistry.Verify(registry => registry.AddPlayer(own), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // A dead hero's row keeps its saved party unchecked, so it can name a party another player's
    // living hero owns. The registry refuses whichever comes second, so the row order would decide.
    // A living hero's row always gets a party of its own from the restorer, so the losing claim is
    // always a dead hero's.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DeadHeroNamesAnotherPlayersParty_RestoresThePartyOwner(bool deadFirst)
    {
        var liveHero = ObjectHelper.SkipConstructor<Hero>();
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var owner = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var deadClaim = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var saved = deadFirst ? new[] { deadClaim, owner } : new[] { owner, deadClaim };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveHero: liveHero,
            liveParty: PartyOwnedBy(liveHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(owner), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // A dead hero waiting for heir selection still owns its party, so its claim wins over a row
    // that names the party without owning it, in either order.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DeadHeroStillOwnsItsParty_KeepsItForHeirSelection(bool deadFirst)
    {
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var heirWaiting = new Player("PlayerOne", DeadHeroId, LivePartyId, "Clan_One", "Character_Dead");
        var otherClaim = new Player("PlayerTwo", LiveHeroId, LivePartyId, "Clan_Two", "Character_Live");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = deadFirst ? new[] { heirWaiting, otherClaim, unrelated } : new[] { otherClaim, heirWaiting, unrelated };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveHero: ObjectHelper.SkipConstructor<Hero>(),
            liveParty: PartyOwnedBy(deadHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(heirWaiting), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Exactly(2));
    }

    [Fact]
    public void AllGameObjectsRegistered_NoClaimantOwnsTheSharedParty_RestoresNeither()
    {
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var second = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            new[] { first, second, unrelated },
            liveHero: ObjectHelper.SkipConstructor<Hero>(),
            liveParty: PartyOwnedBy(ObjectHelper.SkipConstructor<Hero>()),
            deadHero: ObjectHelper.SkipConstructor<Hero>());

        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // When the owner's own row can't be restored, the dead hero's row is the only one left naming the
    // party, in either order and whether the restore fails or throws. It still doesn't own the party.
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void AllGameObjectsRegistered_OwnerRestoreFails_DeadHeroStillDoesNotGetTheParty(bool deadFirst, bool restoreThrows)
    {
        var liveHero = ObjectHelper.SkipConstructor<Hero>();
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var owner = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var deadClaim = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = deadFirst ? new[] { deadClaim, owner, unrelated } : new[] { owner, deadClaim, unrelated };
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        // The real restorer hands back the row it was given even when it fails, so only the failed
        // restore keeps the owner's row out
        var restoredOwner = owner;
        if (restoreThrows)
            playerPartyRestorer
                .Setup(restorer => restorer.TryRestore(owner, out It.Ref<Player>.IsAny))
                .Throws(new InvalidOperationException("party roster broken"));
        else
            playerPartyRestorer.Setup(restorer => restorer.TryRestore(owner, out restoredOwner)).Returns(false);
        var restoredDead = deadClaim;
        var restoredUnrelated = unrelated;
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(deadClaim, out restoredDead)).Returns(true);
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(unrelated, out restoredUnrelated)).Returns(true);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            playerPartyRestorer,
            liveHero: liveHero,
            liveParty: PartyOwnedBy(liveHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(deadClaim), Times.Never);
        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // The two rows spell the party id differently, so grouping by the id string would see two parties
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DeadHeroNamesThePartyByItsOtherId_RestoresOnlyTheOwner(bool deadFirst)
    {
        var liveHero = ObjectHelper.SkipConstructor<Hero>();
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var owner = new Player("PlayerOne", LiveHeroId, LivePartyFullId, "Clan_One", "Character_Live");
        var deadClaim = new Player("PlayerTwo", DeadHeroId, LivePartyId, "Clan_Two", "Character_Dead");
        var saved = deadFirst ? new[] { deadClaim, owner } : new[] { owner, deadClaim };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveHero: liveHero,
            liveParty: PartyOwnedBy(liveHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(owner), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // Two controllers name one dead hero waiting for heir selection and its party, each by a different id,
    // so the id strings don't match. Both claims resolve to the same dead hero and party, and the campaign
    // can't tell which controller owns them, so neither is restored.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_TwoControllersNameOneHeroByDifferentIds_RestoresNeither(bool fullIdsFirst)
    {
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var compact = new Player("PlayerOne", DeadHeroId, DeadPartyId, "Clan_One", "Character_Dead");
        var full = new Player("PlayerTwo", DeadHeroFullId, DeadPartyFullId, "Clan_One", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = fullIdsFirst ? new[] { full, compact, unrelated } : new[] { compact, full, unrelated };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, saved, deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // The same, but each row names a different party the dead hero owns, so the parties don't match either
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_TwoControllersNameOneHeroByDifferentIdsWithTwoParties_RestoresNeither(bool fullIdFirst)
    {
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var compact = new Player("PlayerOne", DeadHeroId, DeadPartyId, "Clan_One", "Character_Dead");
        var full = new Player("PlayerTwo", DeadHeroFullId, LivePartyId, "Clan_One", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = fullIdFirst ? new[] { full, compact, unrelated } : new[] { compact, full, unrelated };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveParty: PartyOwnedBy(deadHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // When a dead hero's party holds another hero, vanilla makes that hero the party's leader and owner,
    // so a dead hero waiting for heir selection doesn't always own its party. No other player owns it
    // then, so the claim is kept.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DeadHerosPartyPassedToACompanion_KeepsItForHeirSelection(bool deadFirst)
    {
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var heirWaiting = new Player("PlayerOne", DeadHeroId, LivePartyId, "Clan_One", "Character_Dead");
        var unrelated = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");
        var saved = deadFirst ? new[] { heirWaiting, unrelated } : new[] { unrelated, heirWaiting };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveParty: PartyOwnedBy(ObjectHelper.SkipConstructor<Hero>()),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(heirWaiting), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(unrelated), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Exactly(2));
    }

    [Theory]
    // One controller's old save can hold a stale row for its dead hero and a row for its living hero, both
    // naming the party the living hero owns. Only the owner's row can keep that party, in either order.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DuplicateControllerDeadRowNamesTheOwnersParty_RestoresTheOwner(bool deadFirst)
    {
        var liveHero = ObjectHelper.SkipConstructor<Hero>();
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var stale = new Player(ControllerId, DeadHeroId, LivePartyId, "Clan_One", "Character_Dead");
        var owner = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var saved = deadFirst ? new[] { stale, owner } : new[] { owner, stale };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveHero: liveHero,
            liveParty: PartyOwnedBy(liveHero),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(owner), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Theory]
    // One controller has a dead row naming a companion's party and a living row of its own. Another
    // controller's dead hero names that party by its full id. Neither dead row can keep the party, and the
    // first controller keeps its living row, whichever of its rows comes first.
    [InlineData(true)]
    [InlineData(false)]
    public void AllGameObjectsRegistered_DuplicateControllerDeadRowSharesAPartyWithAnotherController_RestoresTheLivingRow(bool deadFirst)
    {
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var otherDeadHero = ObjectHelper.SkipConstructor<Hero>();
        otherDeadHero._heroState = Hero.CharacterStates.Dead;
        var deadRow = new Player(ControllerId, DeadHeroId, LivePartyId, "Clan_One", "Character_Dead");
        var livingRow = new Player(ControllerId, StaleHeroId, StalePartyId, "Clan_One", "Character_Stale");
        var otherDeadRow = new Player("PlayerTwo", LiveHeroFullId, LivePartyFullId, "Clan_Two", "Character_Live");
        var saved = deadFirst ? new[] { deadRow, livingRow, otherDeadRow } : new[] { livingRow, deadRow, otherDeadRow };
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            saved,
            liveHero: otherDeadHero,
            liveParty: PartyOwnedBy(ObjectHelper.SkipConstructor<Hero>()),
            deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(livingRow), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(It.IsAny<Player>()), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_DeadHeroWithItsOwnParty_KeepsItForHeirSelection()
    {
        // The usual heir selection case: no other row names the party, and the dead hero still owns it
        var deadHero = ObjectHelper.SkipConstructor<Hero>();
        deadHero._heroState = Hero.CharacterStates.Dead;
        var heirWaiting = new Player("PlayerOne", DeadHeroId, DeadPartyId, "Clan_One", "Character_Dead");
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { heirWaiting }, deadHero: deadHero);

        playerRegistry.Verify(registry => registry.AddPlayer(heirWaiting), Times.Once);
    }

    [Fact]
    public void AllGameObjectsRegistered_DistinctControllers_SkipsUnrestoredRegistration()
    {
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_A");
        var second = new Player("PlayerTwo", MissingHeroId, "Party_B", "Clan_Two", "Character_B");

        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { first, second });

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
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var restoredFirst = first;
        var restoredThird = third;
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(first, out restoredFirst)).Returns(true);
        playerPartyRestorer
            .Setup(restorer => restorer.TryRestore(broken, out It.Ref<Player>.IsAny))
            .Throws(new InvalidOperationException("party roster broken"));
        playerPartyRestorer.Setup(restorer => restorer.TryRestore(third, out restoredThird)).Returns(true);
        var playerRegistry = new Mock<IPlayerManager>();
        playerRegistry.Setup(registry => registry.AddPlayer(It.IsAny<Player>())).Returns(true);

        // TestMessageBroker does not catch subscriber exceptions, so a leaked throw fails here
        using var handler = CreateHandler(
            playerRegistry,
            new[] { first, broken, third },
            playerPartyRestorer,
            deadHero: ObjectHelper.SkipConstructor<Hero>());

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
        playerRegistry
            .Setup(registry => registry.AddPlayer(repaired))
            .Callback(() => calls.Add("register"))
            .Returns(true);

        using var handler = CreateHandler(playerRegistry, new[] { player }, playerPartyRestorer);

        Assert.Equal(new[] { "repair", "register" }, calls);
    }

    [Fact]
    public void AllGameObjectsRegistered_DuplicateStaleParties_PrefersResolvableHeroForRepair()
    {
        var missing = new Player(ControllerId, MissingHeroId, "Party_Missing", "Clan_One", "Character_Missing");
        var recoverable = new Player(ControllerId, LiveHeroId, "Party_Gone", "Clan_One", "Character_Live");
        var repaired = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
        var playerRegistry = new Mock<IPlayerManager>();
        var restoredPlayer = repaired;

        playerPartyRestorer
            .Setup(restorer => restorer.TryRestore(recoverable, out restoredPlayer))
            .Returns(true);
        playerRegistry.Setup(registry => registry.AddPlayer(repaired)).Returns(true);

        using var handler = CreateHandler(
            playerRegistry,
            new[] { missing, recoverable },
            playerPartyRestorer);

        playerRegistry.Verify(registry => registry.AddPlayer(repaired), Times.Once);
        playerRegistry.Verify(registry => registry.AddPlayer(missing), Times.Never);
    }

    /// <summary>
    /// Builds a handler over a loaded session holding <paramref name="savedPlayers"/> and drives it
    /// through the load sequence (GameLoaded, then AllGameObjectsRegistered). The live and stale
    /// heroes and their parties resolve in the object manager, and the dead hero and its party only
    /// when given. Each party is owned by its hero unless the test passes its own live party.
    /// </summary>
    private static SaveGameHandler CreateHandler(
        Mock<IPlayerManager> playerRegistry,
        Player[] savedPlayers,
        Mock<IPlayerPartyRestorer> playerPartyRestorer = null,
        Action<TestMessageBroker>? configureBroker = null,
        Hero liveHero = null!,
        MobileParty liveParty = null!,
        Hero? deadHero = null)
    {
        var messageBroker = new TestMessageBroker();
        if (playerPartyRestorer == null)
        {
            playerPartyRestorer = new Mock<IPlayerPartyRestorer>();
            foreach (var player in savedPlayers)
            {
                var restoredPlayer = player;
                playerPartyRestorer
                    .Setup(restorer => restorer.TryRestore(player, out restoredPlayer))
                    .Returns(player.HeroId is LiveHeroId or LiveHeroFullId or StaleHeroId or DeadHeroId or DeadHeroFullId);
            }
        }
        configureBroker?.Invoke(messageBroker);

        liveHero ??= ObjectHelper.SkipConstructor<Hero>();
        liveParty ??= PartyOwnedBy(liveHero);
        var staleHero = ObjectHelper.SkipConstructor<Hero>();
        var staleParty = PartyOwnedBy(staleHero);
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetObject(LiveHeroId, out liveHero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LiveHeroFullId, out liveHero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LivePartyId, out liveParty)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LivePartyFullId, out liveParty)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(StaleHeroId, out staleHero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(StalePartyId, out staleParty)).Returns(true);
        if (deadHero != null)
        {
            var deadParty = PartyOwnedBy(deadHero);
            objectManager.Setup(manager => manager.TryGetObject(DeadHeroId, out deadHero)).Returns(true);
            objectManager.Setup(manager => manager.TryGetObject(DeadHeroFullId, out deadHero)).Returns(true);
            objectManager.Setup(manager => manager.TryGetObject(DeadPartyId, out deadParty)).Returns(true);
            objectManager.Setup(manager => manager.TryGetObject(DeadPartyFullId, out deadParty)).Returns(true);
        }

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

    private static MobileParty PartyOwnedBy(Hero owner)
    {
        // The constructor reads Campaign.Current, which these tests don't have
        var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
        component.Owner = owner;
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._partyComponent = component;
        return party;
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
            new Mock<IObjectManager>().Object);
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
