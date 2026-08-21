using Common;
using Common.Messaging;
using GameInterface.Serialization;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class HeroInterfaceThreadingTests
{
    static HeroInterfaceThreadingTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void SetupServerHero_GameThreadFailurePropagatesToCaller()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.False(GameThread.Instance.IsGameThread, "the caller must model the network thread");

        var heroInterface = new HeroInterface(
            Mock.Of<IMessageBroker>(),
            Mock.Of<IBinaryPackageFactory>(),
            Mock.Of<IObjectManager>(),
            Mock.Of<IPartyVisibilitySweep>(),
            Mock.Of<IPlayerPartyRestorer>());

        Assert.Throws<NullReferenceException>(() => heroInterface.SetupServerHero(null));
    }
}
