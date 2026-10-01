using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Crime;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Crime;

public class CrimeRatingServiceTests
{
    static CrimeRatingServiceTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void Set_KeepsPlayersSeparateAndPublishesAbsoluteRating()
    {
        var previousServer = ModInformation.IsServer;
        var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
        var objects = new Mock<IObjectManager>();
        var network = new Mock<INetwork>();
        var players = new PlayerManager(Mock.Of<ILogger>(), objects.Object, new ControllerIdProvider());
        var firstHero = ObjectHelper.SkipConstructor<Hero>();
        var secondHero = ObjectHelper.SkipConstructor<Hero>();
        var faction = ObjectHelper.SkipConstructor<Kingdom>();
        var factionId = "Kingdom_empire_s";
        objects.Setup(o => o.TryGetObjectWithLogging<Hero>("firstHero", out firstHero)).Returns(true);
        objects.Setup(o => o.TryGetObjectWithLogging<Hero>("secondHero", out secondHero)).Returns(true);
        objects.Setup(o => o.TryGetId(faction, out factionId)).Returns(true);
        objects.Setup(o => o.TryGetIdWithLogging<IFaction>(faction, out factionId)).Returns(true);
        var first = new Player("first", "firstHero", "", "", "");
        var second = new Player("second", "secondHero", "", "", "");
        players.AddPlayer(first);
        players.AddPlayer(second);
        var ratings = new CrimeRatingService(players, objects.Object, network.Object);
        try
        {
            ModInformation.IsServer = true;
            ResolvedMainHeroContext.ResolvedMainHero = firstHero;
            ratings.Set(faction, 20f);
            Assert.Equal(20f, ratings.Get(faction));
            ResolvedMainHeroContext.ResolvedMainHero = secondHero;
            Assert.Equal(0f, ratings.Get(faction));
            ratings.Set(faction, 5f);
            ratings.Set(faction, float.NaN);
            Assert.Equal(5f, ratings.Get(faction));
            Assert.Equal(20f, first.CrimeRatings[factionId]);
            network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkCrimeRatingChanged
                && ((NetworkCrimeRatingChanged)m).ControllerId == "first"
                && ((NetworkCrimeRatingChanged)m).Rating == 20f)), Times.Once);
            network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Exactly(2));
        }
        finally
        {
            players.RemovePlayer(first);
            players.RemovePlayer(second);
            ResolvedMainHeroContext.ResolvedMainHero = previousHero;
            ModInformation.IsServer = previousServer;
        }
    }
}
