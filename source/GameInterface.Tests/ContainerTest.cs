using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.DynamicSync;
using HarmonyLib;
using Moq;
using Xunit;
using static TaleWorlds.MountAndBlade.GameNetwork.NetworkMessageHandlerRegisterer;

namespace GameInterface.Tests;

public class ContainerTest
{
    [Fact]
    public void Test()
    {
        for (int i = 0; i < 3; i++)
        {
            Harmony harmony = new($"GameInterface.Tests_{i}");
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance();
            containerBuilder.RegisterInstance(harmony).As<Harmony>().SingleInstance();

            RegisterMock<INetwork>(containerBuilder);
            RegisterMock<INetworkConfig>(containerBuilder);
            RegisterMock<ISerializableTypeMapper>(containerBuilder);

            containerBuilder.RegisterModule<GameInterfaceModule>();

            using var module = containerBuilder.Build();

            var gameInterface = module.Resolve<IGameInterface>();
            var dynamicSyncPatcher = module.Resolve<DynamicSyncPatcher>();

            gameInterface.PatchAll();
            gameInterface.UnpatchAll();
        }
    }

    private static void RegisterMock<T>(ContainerBuilder containerBuilder) where T : class
    {
        containerBuilder.RegisterInstance(new Mock<T>().Object).As<T>().SingleInstance();
    }
}
