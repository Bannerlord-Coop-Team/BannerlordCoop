using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Entity;

public class ControllerIdMigrationTests
{
    [Fact]
    public void TryMigrate_PlayerRegistration_RekeysAssociatedIssueState()
    {
        const string legacyControllerId = "123456789";
        const string controllerId = "gog:123456789";
        var playerManager = new Mock<IPlayerManager>();
        var ownershipRegistry = new IssueOwnershipRegistry();
        var troopsRegistry = new AwaitingAlternativeSolutionTroopsRegistry();
        var issueOwner = ObjectHelper.SkipConstructor<Hero>();
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var troops = TroopRoster.CreateDummyTroopRoster();
        troops.AddToCounts(character, 1, false, 0, 0, true);
        var migratedPlayer = new Player(
            controllerId,
            "Hero",
            "Party",
            "Clan",
            "Character");

        ownershipRegistry.SetOwner(issueOwner, legacyControllerId);
        troopsRegistry.Restore(legacyControllerId, troops);
        playerManager
            .Setup(manager => manager.TryMigrateControllerId(
                legacyControllerId,
                controllerId,
                out migratedPlayer))
            .Returns(true);
        var migration = new ControllerIdMigration(
            playerManager.Object,
            ownershipRegistry,
            troopsRegistry);

        Assert.True(migration.TryMigrate(
            legacyControllerId,
            controllerId,
            out var result));

        Assert.Same(migratedPlayer, result);
        Assert.True(ownershipRegistry.TryGetOwnerControllerId(issueOwner, out var issueControllerId));
        Assert.Equal(controllerId, issueControllerId);
        Assert.False(troopsRegistry.TryGet(legacyControllerId, out _));
        Assert.True(troopsRegistry.TryGet(controllerId, out var migratedTroops));
        Assert.Equal(1, migratedTroops.GetTroopCount(character));
    }
}
