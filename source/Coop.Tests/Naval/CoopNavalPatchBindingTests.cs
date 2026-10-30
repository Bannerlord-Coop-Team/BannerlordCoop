#if COOP_NAVAL
using Common;
using Coop.Naval;
using Coop.Naval.CampaignBehaviors.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// Every Coop.Naval Harmony patch still binds to the installed War Sails, and the campaign behavior gates run on the right side.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class CoopNavalPatchBindingTests
{
    // Prefixes that aren't a plain server-only gate
    private static readonly HashSet<MethodInfo> NonServerGates = new()
    {
        AccessTools.Method(typeof(ClanFleetManagementCampaignBehaviorPatches), nameof(ClanFleetManagementCampaignBehaviorPatches.HourlyTickPrefix)),
        AccessTools.Method(typeof(NavalTransitionCampaignBehaviorPatches), nameof(NavalTransitionCampaignBehaviorPatches.SetSailConsequencePrefix)),
        AccessTools.Method(typeof(NavalDLCFigureheadCampaignBehaviorPatches), "OnFigureheadUnlockedPrefix"),
    };

    public static IEnumerable<object[]> PatchClasses => AccessTools.GetTypesFromAssembly(typeof(CoopNavalModule).Assembly)
        .Where(type => HarmonyMethodExtensions.GetFromType(type).Any())
        .Select(type => new object[] { type });

    public static IEnumerable<object[]> ServerGates => AccessTools.GetTypesFromAssembly(typeof(CoopNavalModule).Assembly)
        .Where(type => type.Namespace == typeof(PiratesCampaignBehaviorPatches).Namespace)
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .Where(method => method.IsDefined(typeof(HarmonyPrefix)) && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
        .Where(method => !NonServerGates.Contains(method))
        .Select(method => new object[] { method.DeclaringType, method.Name });

    // A renamed method fails to compile, but a renamed injected parameter or an ambiguous overload only fails when patching
    [Theory]
    [MemberData(nameof(PatchClasses))]
    public void Patch_BindsToTheInstalledNavalDlc(Type patchClass)
    {
        var harmony = new Harmony($"coop.tests.naval.binding.{patchClass.FullName}");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(patchClass).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void PatchClasses_FindsThePatchedNavalBehaviors()
    {
        Assert.Contains(PatchClasses, row => (Type)row[0] == typeof(PiratesCampaignBehaviorPatches));
        Assert.Contains(ServerGates, row => (Type)row[0] == typeof(PiratesCampaignBehaviorPatches));
    }

    // NavalDLC's campaign behaviors run their world simulation on the server only, and clients get the results
    [Theory]
    [MemberData(nameof(ServerGates))]
    public void CampaignBehaviorGate_RunsOnTheServerOnly(Type patchClass, string methodName)
    {
        var gate = AccessTools.Method(patchClass, methodName);
        var wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            Assert.True((bool)gate.Invoke(null, null));

            ModInformation.IsServer = false;
            Assert.False((bool)gate.Invoke(null, null));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // Figurehead unlocks are per player, so each client runs its own
    [Fact]
    public void FigureheadUnlockedGate_RunsOnClientsOnly()
    {
        var gate = AccessTools.Method(typeof(NavalDLCFigureheadCampaignBehaviorPatches), "OnFigureheadUnlockedPrefix");
        var wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            Assert.False((bool)gate.Invoke(null, null));

            ModInformation.IsServer = false;
            Assert.True((bool)gate.Invoke(null, null));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
#endif
