using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Serilog;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class RomanceNpcMarriagePatchesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsClanSuitableForNpcMarriage_NpcClan_PreservesNativeResult(bool nativeResult)
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var expected = nativeResult;

        RomanceNpcMarriagePatches.IsClanSuitableForNpcMarriagePostfix(clan, ref nativeResult);

        Assert.Equal(expected, nativeResult);
    }

    [Fact]
    public void IsClanSuitableForNpcMarriage_OfflinePlayerClan_ReturnsFalse()
    {
        const string clanId = "player-clan";
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var objectManager = new Mock<IObjectManager>();
        Clan resolvedClan = clan;
        objectManager.Setup(manager => manager.TryGetObjectWithLogging<Clan>(clanId, out resolvedClan)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject<Clan>(clanId, out resolvedClan)).Returns(true);
        var playerManager = new PlayerManager(
            Mock.Of<ILogger>(),
            objectManager.Object,
            new ControllerIdProvider());
        var player = new Player("offline-player", string.Empty, string.Empty, clanId, string.Empty);

        Assert.True(playerManager.AddPlayer(player));
        try
        {
            var result = true;

            RomanceNpcMarriagePatches.IsClanSuitableForNpcMarriagePostfix(clan, ref result);

            Assert.False(result);
        }
        finally
        {
            playerManager.RemovePlayer(player);
        }
    }
}
