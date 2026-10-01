using Autofac;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.Services.Players;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests;

[Collection(nameof(CampaignCurrentCollection))]
public class ContainerTest
{
    private const string ContributedPatchCategory = "GameInterface.Tests.ContributedPatches";
    private const string RollbackPatchCategory = "GameInterface.Tests.RollbackPatches";

    [Fact]
    public void Test()
    {
        for (int i = 0; i < 3; i++)
        {
            Harmony harmony = new($"GameInterface.Tests_{i}");
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance();

            RegisterMock<INetwork>(containerBuilder);
            RegisterMock<INetworkConfig>(containerBuilder);
            RegisterMock<ISerializableTypeMapper>(containerBuilder);

            containerBuilder.RegisterModule<GameInterfaceModule>();
            containerBuilder.RegisterInstance(harmony).As<Harmony>().SingleInstance();

            try
            {
                using var module = containerBuilder.Build();

                var gameInterface = module.Resolve<IGameInterface>();
                var AutoSyncPatcher = module.Resolve<AutoSyncPatcher>();
                module.Resolve<IPlayerPartyRestorer>();
                ICoopCommand[] commands = module.Resolve<IEnumerable<ICoopCommand>>().ToArray();
                Assert.Contains(commands, command =>
                    $"{command.Prefix}.{command.Name}" == "coop.debug.hero.set_gold");
                Assert.Contains(commands, command =>
                    $"{command.Prefix}.{command.Name}" == "coop.debug.kingdom.add_decision");

                gameInterface.PatchAll();
            }
            finally
            {
                // Production keeps patches through disconnects; tests must release their own patches.
                harmony.UnpatchAll(harmony.Id);
            }
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

    [Fact]
    public void PatchAll_AfterAFailedAttempt_RefusesToReportSuccess()
    {
        Harmony harmony = new($"{nameof(PatchAll_AfterAFailedAttempt_RefusesToReportSuccess)}.{Guid.NewGuid()}");
        // Throws in the category loop, after the uncategorized patches and before AutoSync's static assembly.
        var failingCategory = new HarmonyPatchCategoryRegistration(assembly: null, "GameInterface.Tests.FailingCategory");

        try
        {
            Exception? firstFailure;
            using (IContainer failedStart = BuildContainer(harmony, failingCategory))
            {
                firstFailure = Record.Exception(failedStart.Resolve<IGameInterface>().PatchAll);
            }

            Assert.NotNull(firstFailure);
            Assert.True(Harmony.HasAnyPatches(harmony.Id));

            using IContainer retry = BuildContainer(harmony);
            var refused = Assert.Throws<InvalidOperationException>(retry.Resolve<IGameInterface>().PatchAll);
            Assert.Contains("Restart Bannerlord", refused.Message);
            Assert.Same(firstFailure, refused.InnerException);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void PatchAll_AfterAFailedAutoSyncBind_RefusesToReportSuccess()
    {
        Harmony harmony = new($"{nameof(PatchAll_AfterAFailedAutoSyncBind_RefusesToReportSuccess)}.{Guid.NewGuid()}");
        var typeMapper = new Mock<ISerializableTypeMapper>();

        try
        {
            Exception? firstFailure;
            using (IContainer failedStart = BuildContainer(harmony, typeMapper: typeMapper.Object))
            {
                // Registries add their types while the container builds, so only the AutoSync bind after it throws.
                typeMapper.Setup(mapper => mapper.AddTypes(It.IsAny<IEnumerable<Type>>()))
                    .Throws(new InvalidOperationException("AutoSync bind failed"));

                firstFailure = Record.Exception(failedStart.Resolve<IGameInterface>().PatchAll);
            }

            // Build and the AutoSync patches went in; only the handler bind failed.
            Assert.Equal("AutoSync bind failed", firstFailure?.Message);

            using IContainer retry = BuildContainer(harmony);
            var refused = Assert.Throws<InvalidOperationException>(retry.Resolve<IGameInterface>().PatchAll);
            Assert.Contains("Restart Bannerlord", refused.Message);
            Assert.Same(firstFailure, refused.InnerException);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void PatchAll_AfterAFailureThatLeftNoPatches_RunsTheFullInstallAgain()
    {
        Harmony harmony = new($"{nameof(PatchAll_AfterAFailureThatLeftNoPatches_RunsTheFullInstallAgain)}.{Guid.NewGuid()}");
        var rollbackCategory = new HarmonyPatchCategoryRegistration(typeof(ContainerTest).Assembly, RollbackPatchCategory);

        try
        {
            Exception? firstFailure;
            using (IContainer failedStart = BuildContainer(harmony, rollbackCategory))
            {
                firstFailure = Record.Exception(failedStart.Resolve<IGameInterface>().PatchAll);
            }

            // Same state as a failure before the first patch.
            Assert.Equal("Failed with nothing applied", firstFailure?.GetBaseException().Message);
            Assert.False(Harmony.HasAnyPatches(harmony.Id));

            using IContainer retry = BuildContainer(harmony);
            retry.Resolve<IGameInterface>().PatchAll();

            Assert.True(Harmony.HasAnyPatches(harmony.Id));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static IContainer BuildContainer(
        Harmony harmony,
        HarmonyPatchCategoryRegistration? patchCategory = null,
        ISerializableTypeMapper? typeMapper = null)
    {
        var containerBuilder = new ContainerBuilder();

        containerBuilder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance();

        RegisterMock<INetwork>(containerBuilder);
        RegisterMock<INetworkConfig>(containerBuilder);
        containerBuilder.RegisterInstance(typeMapper ?? new Mock<ISerializableTypeMapper>().Object)
            .As<ISerializableTypeMapper>()
            .SingleInstance();

        containerBuilder.RegisterModule<GameInterfaceModule>();
        containerBuilder.RegisterInstance(harmony).As<Harmony>().SingleInstance();
        if (patchCategory != null)
        {
            containerBuilder.RegisterInstance(patchCategory);
        }

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

    // Removes every patch of the harmony applying it, then fails, so nothing of that attempt stays applied.
    [HarmonyPatch(typeof(ContainerTest), nameof(ContributedPatchTarget))]
    [HarmonyPatchCategory(RollbackPatchCategory)]
    private static class RollbackPatch
    {
        [HarmonyPrepare]
        private static bool Prepare(Harmony instance)
        {
            // The test bootstrap patches every class in this assembly.
            if (!instance.Id.StartsWith(nameof(PatchAll_AfterAFailureThatLeftNoPatches_RunsTheFullInstallAgain), StringComparison.Ordinal)) return false;

            instance.UnpatchAll(instance.Id);
            throw new InvalidOperationException("Failed with nothing applied");
        }

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
