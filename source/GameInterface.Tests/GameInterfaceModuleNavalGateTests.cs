using Autofac;
using Autofac.Core;
using Common;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Covers that GameInterface's own War Sails patches and registrations follow <see cref="ModInformation.IsNavalDlcActive"/>.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public sealed class GameInterfaceModuleNavalGateTests : IDisposable
{
    // Namespaces whose code exists only for War Sails, so every patch and registration in them must be gated.
    private static readonly string[] NavalOnlyNamespaces =
    {
        "GameInterface.Services.Ships",
        "GameInterface.Services.Rafts",
        "GameInterface.Services.NavalDLC",
    };

    private readonly bool previousNavalDlc = ModInformation.IsNavalDlcActive;

    // The game's own module constructor reads the startup decision, so off must register and install nothing naval.
    // The on direction is covered below without the default constructor, which would also load the optional naval assemblies.
    [Fact]
    public void DefaultModule_WithNavalInactive_RegistersAndInstallsNothingNaval()
    {
        ModInformation.IsNavalDlcActive = false;

        AssertNavalServices(new GameInterfaceModule(), expected: false);
    }

    // The module's decision reaches both the naval registrations and the installer's naval category.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Module_GatesNavalRegistrationsAndPatchesTogether(bool navalServicesEnabled)
    {
        AssertNavalServices(new GameInterfaceModule(navalServicesEnabled), navalServicesEnabled);
    }

    // Builds the module without activating anything and checks its naval registrations and installed naval patches.
    private static void AssertNavalServices(GameInterfaceModule module, bool expected)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(module);
        using var container = builder.Build(Autofac.Builder.ContainerBuildOptions.IgnoreStartableComponents);

        Assert.All(GameInterfaceModule.NavalOnlyTypes, type => Assert.Equal(expected, container.IsRegistered(type)));

        var harmony = new Harmony($"{nameof(GameInterfaceModuleNavalGateTests)}.{Guid.NewGuid():N}");
        try
        {
            container.Resolve<IGameInterfacePatchInstaller>().Install(harmony);

            var navalPatches = GameInterfacePatchInstallerTests.CategoryPatchesOwnedBy(harmony, GameInterface.HARMONY_NAVAL_CATEGORY);
            Assert.Equal(expected, navalPatches.Count > 0);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A new patch class in a naval-only namespace must join the naval category, otherwise it is applied with naval off.
    [Fact]
    public void NavalOnlyNamespaces_PatchClassesAreAllInTheNavalCategory()
    {
        var uncategorized = NavalOnlyTypesInAssembly()
            .Where(type => type.GetCustomAttribute<HarmonyPatch>() != null)
            .Where(type => type.GetCustomAttribute<HarmonyPatchCategory>()?.info.category != GameInterface.HARMONY_NAVAL_CATEGORY)
            .Select(type => type.FullName);

        Assert.Empty(uncategorized);
    }

    // A new AutoSync class or registry in a naval-only namespace must be listed, otherwise it registers with naval off.
    [Fact]
    public void NavalOnlyNamespaces_SyncAndRegistriesAreAllListed()
    {
        var unlisted = NavalOnlyTypesInAssembly()
            .Where(type => !type.IsAbstract && (typeof(IAutoSync).IsAssignableFrom(type) || IsAutoRegistry(type)))
            .Where(type => !GameInterfaceModule.NavalOnlyTypes.Contains(type))
            .Select(type => type.FullName);

        Assert.Empty(unlisted);
    }

    // GameInterface types declared in one of the naval-only namespaces.
    private static Type[] NavalOnlyTypesInAssembly() =>
        typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.Namespace != null && NavalOnlyNamespaces.Any(ns => type.Namespace == ns || type.Namespace.StartsWith(ns + ".")))
            .ToArray();

    // Whether a type is an auto registry that RegistryModule would pick up.
    private static bool IsAutoRegistry(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>));

    public void Dispose()
    {
        ModInformation.IsNavalDlcActive = previousNavalDlc;
    }
}
