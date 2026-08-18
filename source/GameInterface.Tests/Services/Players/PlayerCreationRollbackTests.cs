using Common.Network.Coalescing;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Serilog;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerCreationRollbackTests
{
    [Fact]
    public void Rollback_RemovesCapturedChildAfterPartyRegistrationWasAlreadyDestroyed()
    {
        var objectManager = new global::GameInterface.Services.ObjectManager.ObjectManager(
            new Mock<ILogger>().Object);
        var coalescer = new Mock<ISendCoalescer>();
        var rollback = new PlayerCreationRollback(objectManager, coalescer.Object);
        var hero = Uninitialized<Hero>();
        var party = Uninitialized<MobileParty>();
        var clan = Uninitialized<Clan>();
        var characterObject = Uninitialized<CharacterObject>();
        var orphanedRoster = new object();

        hero._partyBelongedTo = party;
        hero._clan = clan;
        hero._characterObject = characterObject;

        Assert.True(objectManager.AddExisting("Hero_player", hero));
        Assert.True(objectManager.AddExisting("MobileParty_player", party));
        Assert.True(objectManager.AddExisting("Clan_player", clan));
        Assert.True(objectManager.AddExisting("CharacterObject_player", characterObject));
        Assert.True(objectManager.AddExisting("TroopRoster_MemberRoster_player", orphanedRoster));

        var player = new Player(
            "controller",
            "Hero_player",
            "MobileParty_player",
            "Clan_player",
            "CharacterObject_player");

        var registrationIds = rollback.CaptureRegistrationIds(player)
            .Append("TroopRoster_MemberRoster_player")
            .ToArray();
        Assert.True(objectManager.Remove(party));
        rollback.Rollback(player, registrationIds);

        Assert.False(objectManager.Contains(hero));
        Assert.False(objectManager.Contains(party));
        Assert.False(objectManager.Contains(clan));
        Assert.False(objectManager.Contains(characterObject));
        Assert.False(objectManager.Contains(orphanedRoster));
        Assert.Contains("Hero_player", registrationIds);
        Assert.Contains("MobileParty_player", registrationIds);
        coalescer.Verify(instance => instance.DropInstance("Hero_player"), Times.Once);
        coalescer.Verify(instance => instance.DropInstance("player"), Times.AtLeastOnce);
    }

    private static T Uninitialized<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));
}
