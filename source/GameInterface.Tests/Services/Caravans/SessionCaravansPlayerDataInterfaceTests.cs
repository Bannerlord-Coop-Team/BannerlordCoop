using Common;
using Coop.Tests.Mocks;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Caravans;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Caravans;

public class SessionCaravansPlayerDataInterfaceTests
{
    static SessionCaravansPlayerDataInterfaceTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void UpdateTradeRumorTakenCaravans_NullUpdateStoresEmptyDictionary()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        var tradeRumorTakenCaravans = new Dictionary<string, Dictionary<string, long>>();
        var coopSession = new Mock<ICoopSession>();
        coopSession.SetupGet(session => session.CaravansPlayerData)
            .Returns(new CaravansPlayerData(new Dictionary<string, List<string>>(), tradeRumorTakenCaravans));
        var provider = new Mock<ICoopSessionProvider>();
        provider.SetupGet(session => session.CoopSession).Returns(coopSession.Object);
        var objectManager = new Mock<IObjectManager>();
        Hero hero = null;
        objectManager.Setup(manager => manager.TryGetObjectWithLogging("player-hero", out hero)).Returns(true);
        var sessionInterface = new SessionCaravansPlayerDataInterface(provider.Object, objectManager.Object, Mock.Of<IPlayerManager>());

        // A client with no taken rumors sends its empty dictionary, which protobuf delivers as null.
        sessionInterface.UpdateTradeRumorTakenCaravansForPlayer("player-hero", null);
        GameThread.Run(() => { }, blocking: true);

        Assert.True(tradeRumorTakenCaravans.TryGetValue("player-hero", out var stored));
        Assert.NotNull(stored);
        Assert.Empty(stored);
    }
}
