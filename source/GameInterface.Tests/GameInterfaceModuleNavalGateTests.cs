using Autofac;
using Common;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Services;
using GameInterface.Services.AnchorPoints;
using GameInterface.Services.AnchorPoints.Handlers;
using GameInterface.Services.AnchorPoints.Patches;
using GameInterface.Services.MapEventComponents;
using GameInterface.Services.MapEventComponents.Handlers;
using GameInterface.Services.MapEventComponents.Patches;
using GameInterface.Services.MapEvents.Interfaces;
#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
#endif
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.PartyVisuals.Patches;
using GameInterface.Services.Rafts.Commands;
using GameInterface.Services.Rafts.Handlers;
using GameInterface.Services.Rafts.Interfaces;
using GameInterface.Services.Rafts.Patches;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Figureheads;
using GameInterface.Services.Ships.Handlers;
using GameInterface.Services.Ships.Patches;
using GameInterface.Services.Ships.ShipHulls;
using GameInterface.Services.Ships.ShipSlots;
using GameInterface.Services.Ships.ShipUpgradePieces;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using Common.Commands;
using Common.Messaging;
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
    // Written out by hand, not taken from GameInterfaceModule.NavalOnlyTypes, so a type dropped from the gate fails here.
    private static readonly Type[] ExpectedNavalRegistrations =
    {
        typeof(ShipSync),
        typeof(ShipRegistry),
        typeof(ShipHullRegistry),
        typeof(FigureheadRegistry),
        typeof(ShipSlotRegistry),
        typeof(ShipUpgradePieceRegistry),
        typeof(BlockadeBattleMapEventRegistry),
        typeof(LootShipHoldHandler),
        typeof(LootShipsHandler),
        typeof(ShipPiecesHandler),
        typeof(PlayerRaftHandler),
        typeof(AnchorPointHandler),
        typeof(PlayerBlockadeHandler),
        typeof(SiegeBlockadeHandler),
        typeof(PlayerRaftFleetInterface),
        typeof(RaftDebugCommands.StrandOnRaftCoopCommand),
        typeof(BattleShipFateLedger),
        typeof(BattleShipFateApplier),
#if DEBUG
        typeof(NavalScenarioClientHandler),
        typeof(NavalScenarioRejoinHandler),
        typeof(NavalScenarioCoopCommand),
        typeof(StartNavalCoopCommand),
        typeof(NavalScenarioState),
        typeof(NavalScenarioResolver),
        typeof(NavalScenarioStager),
        typeof(NavalBattleStaging),
        typeof(NavalEngageResetTracer),
#endif
    };

    // Written out by hand, including the naval classes that live in shared land folders.
    private static readonly Type[] ExpectedNavalPatchClasses =
    {
        typeof(PlayerEncounterLootShipsPatches),
        typeof(ShipFigureheadPatches),
        typeof(ShipHitPointsPatches),
        typeof(ShipLifetimePatches),
        typeof(ShipMainPartyPatches),
        typeof(ShipPiecesPatches),
        typeof(PlayerRaftFleetPatches),
        typeof(PlayerRaftVisibilityPatches),
        typeof(AnchorPointPatches),
        typeof(AnchorPointMenuRefreshPatch),
        typeof(PlayerLandSeaTransitionPatches),
        typeof(LandSeaTransitionFinishPatch),
        typeof(PlayerLandSeaTransitionCancelPatch),
        typeof(PlayerLandSeaPathPatch),
        typeof(MobilePartyRaftPatches),
        typeof(PlayerVillageLandSeaLeavePatch),
        typeof(PlayerNavalTownLeavePatch),
        typeof(PlayerBlockadePatches),
        typeof(PlayerBlockadeMenuPatches),
        typeof(SiegeBlockadePatches),
        typeof(MobilePartyBlockadeAnchorPatches),
        typeof(NavalMobilePartyVisualManagerPatches),
#if DEBUG
        typeof(NavalEngageResetTracePatch),
#endif
    };

    // Shared with land games, so they must stay registered while naval is off.
    private static readonly Type[] SharedLandRegistrations =
    {
        typeof(AnchorPointRegistry),
        typeof(AnchorPointSync),
        typeof(LootShipHold),
        typeof(MapEventResultsInterface),
    };

    // Namespaces whose code exists only for War Sails, so every scanned registration in them must be gated.
    private static readonly string[] NavalOnlyNamespaces =
    {
        "GameInterface.Services.Ships",
        "GameInterface.Services.Rafts",
        "GameInterface.Services.NavalDLC",
    };

    private readonly bool previousNavalDlc = ModInformation.IsNavalDlcActive;

    // The game's own module constructor reads the startup decision, so off must register and install nothing naval.
    [Fact]
    public void DefaultModule_WithNavalInactive_RegistersAndInstallsNothingNaval()
    {
        ModInformation.IsNavalDlcActive = false;

        AssertNavalServices(new GameInterfaceModule(), expected: false);
    }

    // The module's decision reaches the naval registrations, handlers, abstractions, commands and patches together.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Module_GatesNavalRegistrationsAndPatchesTogether(bool navalServicesEnabled)
    {
        AssertNavalServices(new GameInterfaceModule(navalServicesEnabled), navalServicesEnabled);
    }

    // A naval patch class without the naval category would be installed with naval off.
    [Fact]
    public void ExpectedNavalPatchClasses_AreAllInTheNavalCategory()
    {
        var uncategorized = ExpectedNavalPatchClasses
            .Where(type => type.GetCustomAttribute<HarmonyPatchCategory>()?.info.category != GameInterface.HARMONY_NAVAL_CATEGORY)
            .Select(type => type.FullName);

        Assert.Empty(uncategorized);
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

    // A new scanned registration in a naval-only namespace must be gated, otherwise it registers with naval off.
    [Fact]
    public void NavalOnlyNamespaces_ScannedRegistrationsAreAllGated()
    {
        var ungated = NavalOnlyTypesInAssembly()
            .Where(type => type.IsClass && !type.IsAbstract && IsScannedRegistration(type))
            .Where(type => GameInterfaceModule.ShouldRegister(type, navalServicesEnabled: false))
            .Select(type => type.FullName);

        Assert.Empty(ungated);
    }

    // Builds the module without activating anything and checks its naval registrations and installed naval patches.
    private static void AssertNavalServices(GameInterfaceModule module, bool expected)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(module);
        using var container = builder.Build(Autofac.Builder.ContainerBuildOptions.IgnoreStartableComponents);

        var registeredTypes = container.ComponentRegistry.Registrations
            .Select(registration => registration.Activator.LimitType)
            .ToHashSet();
        Assert.All(ExpectedNavalRegistrations, type => Assert.True(expected == registeredTypes.Contains(type), type.FullName));
        Assert.All(SharedLandRegistrations, type => Assert.True(registeredTypes.Contains(type), type.FullName));
        Assert.Equal(expected, container.IsRegistered<IPlayerRaftFleetInterface>());
        Assert.Equal(expected, container.IsRegistered<IBattleShipFateApplier>());

        var harmony = new Harmony($"{nameof(GameInterfaceModuleNavalGateTests)}.{Guid.NewGuid():N}");
        try
        {
            container.Resolve<IGameInterfacePatchInstaller>().Install(harmony);

            var installedNavalClasses = harmony.GetPatchedMethods()
                .Select(Harmony.GetPatchInfo)
                .SelectMany(info => info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
                .Where(patch => patch.owner == harmony.Id)
                .Select(patch => patch.PatchMethod.DeclaringType)
                .Where(type => ExpectedNavalPatchClasses.Contains(type))
                .Distinct()
                .ToList();
            Assert.Equal(expected, installedNavalClasses.Count > 0);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // GameInterface types declared in one of the naval-only namespaces, nested types included.
    private static Type[] NavalOnlyTypesInAssembly() =>
        typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.Namespace != null && NavalOnlyNamespaces.Any(ns => type.Namespace == ns || type.Namespace.StartsWith(ns + ".")))
            .ToArray();

    // Whether one of GameInterface's assembly scans would register this type.
    private static bool IsScannedRegistration(Type type) =>
        typeof(IHandler).IsAssignableFrom(type) ||
        typeof(IGameAbstraction).IsAssignableFrom(type) ||
        typeof(ICoopCommand).IsAssignableFrom(type) ||
        typeof(IAutoSync).IsAssignableFrom(type) ||
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>));

    public void Dispose()
    {
        ModInformation.IsNavalDlcActive = previousNavalDlc;
    }
}
