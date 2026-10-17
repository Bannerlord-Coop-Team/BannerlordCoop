using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Notifications.Patches;
using Moq;
using Serilog;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class CharacterNotificationPatchesTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, true)]
    public void SkillNotification_OnlyExcludesOtherPlayersOnClients(
        bool isServer, bool isPlayer, bool isLocalPlayer, bool expected)
    {
        var wasServer = ModInformation.IsServer;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var objects = new Mock<IObjectManager>();
        objects.Setup(manager => manager.TryGetObject<Hero>("hero", out hero)).Returns(true);
        objects.Setup(manager => manager.TryGetObjectWithLogging<Hero>("hero", out hero)).Returns(true);
        var controller = new ControllerIdProvider();
        controller.SetControllerId(isLocalPlayer ? "player" : "other");
        var players = new PlayerManager(Mock.Of<ILogger>(), objects.Object, controller);
        var player = new Player("player", "hero", string.Empty, string.Empty, string.Empty);
        if (isPlayer) Assert.True(players.AddPlayer(player));
        try
        {
            ModInformation.IsServer = isServer;
            Assert.Equal(expected, CharacterNotificationPatches.Prefix(hero));
        }
        finally
        {
            if (isPlayer) players.RemovePlayer(player);
            ModInformation.IsServer = wasServer;
        }
    }
}
