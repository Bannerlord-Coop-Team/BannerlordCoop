using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.AutoSync;
using HarmonyLib;
using Moq;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests;

public class ContainerTest
{
    private const string ContributedPatchCategory = "GameInterface.Tests.ContributedPatches";

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
            var AutoSyncPatcher = module.Resolve<AutoSyncPatcher>();

            gameInterface.PatchAll();
            gameInterface.UnpatchAll();
        }
    }

    [Fact]
    public void PatchAll_DoesNotReapplyContributedCategoryOnReconnect()
    {
        Harmony harmony = new($"{nameof(PatchAll_DoesNotReapplyContributedCategoryOnReconnect)}.{Guid.NewGuid()}");
        var patchCategory = new HarmonyPatchCategoryRegistration(
            typeof(ContainerTest).Assembly,
            ContributedPatchCategory);

        try
        {
            patchCategory.Apply(harmony);
            AssertContributedPatchCount(harmony, 1);

            using IContainer reconnectContainer = BuildContainer(harmony, patchCategory);
            reconnectContainer.Resolve<IGameInterface>().PatchAll();

            AssertContributedPatchCount(harmony, 1);
        }
        finally
        {
            harmony.Unpatch(
                AccessTools.Method(typeof(ContainerTest), nameof(ContributedPatchTarget)),
                HarmonyPatchType.All,
                harmony.Id);
        }
    }

    private static IContainer BuildContainer(
        Harmony harmony,
        HarmonyPatchCategoryRegistration patchCategory)
    {
        var containerBuilder = new ContainerBuilder();

        containerBuilder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance();

        RegisterMock<INetwork>(containerBuilder);
        RegisterMock<INetworkConfig>(containerBuilder);
        RegisterMock<ISerializableTypeMapper>(containerBuilder);

        containerBuilder.RegisterModule<GameInterfaceModule>();
        containerBuilder.RegisterInstance(harmony).As<Harmony>().SingleInstance();
        containerBuilder.RegisterInstance(patchCategory);

        return containerBuilder.Build();
    }

    private static void AssertContributedPatchCount(Harmony harmony, int expected)
    {
        var target = AccessTools.Method(typeof(ContainerTest), nameof(ContributedPatchTarget));
        var patchInfo = Harmony.GetPatchInfo(target);

        Assert.Equal(expected, patchInfo.Prefixes.Count(patch => patch.owner == harmony.Id));
    }

    private static int contributedPatchValue;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ContributedPatchTarget(int value) => contributedPatchValue = value;

    [HarmonyPatch(typeof(ContainerTest), nameof(ContributedPatchTarget))]
    [HarmonyPatchCategory(ContributedPatchCategory)]
    private static class ContributedPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
        }
    }

    private static void RegisterMock<T>(ContainerBuilder containerBuilder) where T : class
    {
        containerBuilder.RegisterInstance(new Mock<T>().Object).As<T>().SingleInstance();
    }
}
