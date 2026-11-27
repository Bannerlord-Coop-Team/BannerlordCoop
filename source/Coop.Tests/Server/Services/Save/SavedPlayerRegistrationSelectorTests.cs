using Common.Util;
using Coop.Core.Server.Services.Save;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class SavedPlayerRegistrationSelectorTests
{
    private const string ControllerId = "PlayerOne";
    private const string LiveHeroId = "Hero_Live";
    private const string LivePartyId = "Party_Live";
    // The object manager resolves an id with or without its type prefix, so both name the same hero
    private const string LiveHeroFullId = "Hero_" + LiveHeroId;
    private const string StaleHeroId = "Hero_Stale";
    private const string StalePartyId = "Party_Stale";
    private const string MissingHeroId = "Hero_Missing";

    private readonly Hero liveHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero staleHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly MobileParty liveParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly MobileParty staleParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly Mock<IPlayerPartyRestorer> playerPartyRestorer = new Mock<IPlayerPartyRestorer>();

    [Theory]
    // A save written before controller ids were unique can hold the duplicates in either order,
    // and the dead one comes first as often as not.
    [InlineData(true)]
    [InlineData(false)]
    public void Select_DuplicateController_KeepsTheRowWhoseGraphExists(bool missingFirst)
    {
        var missing = new Player(ControllerId, MissingHeroId, "Party_Missing", "Clan_One", "Character_Missing");
        var live = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");

        var rows = Select(missingFirst ? new[] { missing, live } : new[] { live, missing });

        Assert.Equal(new[] { live }, rows);
    }

    [Fact]
    public void Select_DuplicateController_DropsTheRowWithAMissingParty()
    {
        var stale = new Player(ControllerId, StaleHeroId, "Party_Gone", "Clan_One", "Character_Stale");
        var live = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");

        var rows = Select(stale, live);

        Assert.Equal(new[] { live }, rows);
    }

    [Fact]
    public void Select_DuplicateControllerWithStaleParties_KeepsTheRowWhoseHeroExists()
    {
        var missing = new Player(ControllerId, MissingHeroId, "Party_Missing", "Clan_One", "Character_Missing");
        var recoverable = new Player(ControllerId, LiveHeroId, "Party_Gone", "Clan_One", "Character_Live");

        var rows = Select(missing, recoverable);

        Assert.Equal(new[] { recoverable }, rows);
    }

    [Theory]
    // One controller's old save can hold a stale row and a row for its living hero, both naming the party the living
    // hero owns. Only the owner's row can keep that party, in either order.
    [InlineData(true)]
    [InlineData(false)]
    public void Select_DuplicateController_KeepsTheRowThatOwnsItsParty(bool staleFirst)
    {
        var stale = new Player(ControllerId, StaleHeroId, LivePartyId, "Clan_One", "Character_Stale");
        var owner = new Player(ControllerId, LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        Owns(owner, liveParty);

        var rows = Select(staleFirst ? new[] { stale, owner } : new[] { owner, stale });

        Assert.Equal(new[] { owner }, rows);
    }

    [Theory]
    // The file can't tell which controller really owns the hero, so neither gets it, whichever id each row spells
    [InlineData(LiveHeroId)]
    [InlineData(LiveHeroFullId)]
    public void Select_TwoControllersClaimOneHero_SelectsNeither(string secondHeroId)
    {
        var first = new Player("PlayerOne", LiveHeroId, LivePartyId, "Clan_One", "Character_Live");
        var second = new Player("PlayerTwo", secondHeroId, LivePartyId, "Clan_One", "Character_Live");
        var other = new Player("PlayerThree", StaleHeroId, StalePartyId, "Clan_Three", "Character_Stale");

        var rows = Select(first, second, other);

        Assert.Equal(new[] { other }, rows);
    }

    [Theory]
    // Both of PlayerOne's rows have a live graph, so picking one row per controller first would let the row order or
    // the id the dropped row spells decide whether the shared hero knocks out PlayerOne's other hero too.
    [InlineData(LiveHeroId, true)]
    [InlineData(LiveHeroId, false)]
    [InlineData(LiveHeroFullId, true)]
    [InlineData(LiveHeroFullId, false)]
    public void Select_SharedHeroInDuplicateController_KeepsTheControllersOtherHero(string sharedHeroId, bool sharedFirst)
    {
        var own = new Player("PlayerOne", StaleHeroId, StalePartyId, "Clan_One", "Character_Stale");
        var shared = new Player("PlayerOne", sharedHeroId, LivePartyId, "Clan_One", "Character_Live");
        var claimed = new Player("PlayerTwo", LiveHeroId, LivePartyId, "Clan_Two", "Character_Live");

        var rows = Select(sharedFirst ? new[] { shared, own, claimed } : new[] { own, shared, claimed });

        Assert.Equal(new[] { own }, rows);
    }

    [Theory]
    // Two controllers' dead heroes can both still be given one party, which neither then owns. The first controller
    // keeps its living row, whichever of its rows comes first.
    [InlineData(true)]
    [InlineData(false)]
    public void Select_PartySharedWithAnotherController_KeepsTheControllersOtherRow(bool sharedFirst)
    {
        var sharedRow = new Player(ControllerId, "Hero_Dead", LivePartyId, "Clan_One", "Character_Dead");
        var livingRow = new Player(ControllerId, StaleHeroId, StalePartyId, "Clan_One", "Character_Stale");
        var otherDeadRow = new Player("PlayerTwo", "Hero_OtherDead", LivePartyId, "Clan_Two", "Character_OtherDead");
        Owns(sharedRow, liveParty);
        Owns(livingRow, staleParty);
        Owns(otherDeadRow, liveParty);

        var rows = Select(sharedFirst ? new[] { sharedRow, livingRow, otherDeadRow } : new[] { livingRow, sharedRow, otherDeadRow });

        Assert.Equal(new[] { livingRow }, rows);
    }

    // The restorer says the campaign still gives the row this party
    private void Owns(Player row, MobileParty party) =>
        playerPartyRestorer
            .Setup(restorer => restorer.TryGetOwnedParty(row, It.IsAny<IReadOnlyCollection<Hero>>(), out party))
            .Returns(true);

    // The live and stale heroes and their parties resolve in the object manager
    private IReadOnlyList<Player> Select(params Player[] savedPlayers)
    {
        var live = liveHero;
        var stale = staleHero;
        var liveHeroParty = liveParty;
        var staleHeroParty = staleParty;
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetObject(LiveHeroId, out live)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LiveHeroFullId, out live)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(StaleHeroId, out stale)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(LivePartyId, out liveHeroParty)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(StalePartyId, out staleHeroParty)).Returns(true);

        return new SavedPlayerRegistrationSelector(objectManager.Object, playerPartyRestorer.Object).Select(savedPlayers);
    }
}
