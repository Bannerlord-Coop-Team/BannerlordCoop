using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Patches;

/// <summary>
/// Fills the side-dependent interior of a replicated <see cref="SiegeEnginesContainer"/> shell.
/// Client shells are created without running the constructor, and the battle side is only known
/// where the container is assigned to its owner: BesiegerCamp = attacker, Settlement = defender.
/// </summary>
[HarmonyPatch]
internal class SiegeEnginesContainerShellPatches
{
    [HarmonyPatch(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines), MethodType.Setter)]
    [HarmonyPostfix]
    private static void BesiegerCampSetterPostfix(BesiegerCamp __instance)
    {
        InitializeShell(__instance.SiegeEngines, BattleSideEnum.Attacker);
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.SiegeEngines), MethodType.Setter)]
    [HarmonyPostfix]
    private static void SettlementSetterPostfix(Settlement __instance)
    {
        InitializeShell(__instance.SiegeEngines, BattleSideEnum.Defender);

        // Vanilla allocates this in InitializeSiegeEventSide, which only the server's siege start runs; the
        // settlement's map visual derefs the list every refresh, so a client left with null aborts the refresh
        // and the siege platform meshes never become hittable. Not readonly, so assign it directly.
        if (__instance._siegeEngineMissiles == null)
        {
            __instance._siegeEngineMissiles = new MBList<SiegeEngineMissile>();
        }
    }

    private static void InitializeShell(SiegeEnginesContainer container, BattleSideEnum side)
    {
        // Server containers are constructor-built; only an unfilled client shell has a null list.
        if (container == null || container._deployedSiegeEngines != null) return;

        var deployedCounts = new Dictionary<SiegeEngineType, int>();
        var reservedCounts = new Dictionary<SiegeEngineType, int>();

        // The publicizer exposes these fields for reading (the null guard above), but it keeps their
        // readonly modifier, so a direct write still won't compile (CS0191) — set them by reflection.
        // The count properties have publicized setters, so those are assigned directly below.
        SetReadonlyField(container, nameof(SiegeEnginesContainer._deployedSiegeEngines), new MBList<SiegeEngineConstructionProgress>(4));
        SetReadonlyField(container, nameof(SiegeEnginesContainer.DeployedRangedSiegeEngines), new SiegeEngineConstructionProgress[4]);
        SetReadonlyField(container, nameof(SiegeEnginesContainer.DeployedMeleeSiegeEngines),
            new SiegeEngineConstructionProgress[side == BattleSideEnum.Attacker ? 3 : 0]);
        SetReadonlyField(container, nameof(SiegeEnginesContainer._reservedSiegeEngines), new MBList<SiegeEngineConstructionProgress>());
        SetReadonlyField(container, nameof(SiegeEnginesContainer._removedSiegeEngines), new MBList<SiegeEnginesContainer.RemovedSiegeEngine>());
        SetReadonlyField(container, nameof(SiegeEnginesContainer._deployedSiegeEngineTypesCount), deployedCounts);
        SetReadonlyField(container, nameof(SiegeEnginesContainer._reservedSiegeEngineTypesCount), reservedCounts);

        container.DeployedSiegeEngineTypesCount = new MBReadOnlyDictionary<SiegeEngineType, int>(deployedCounts);
        container.ReservedSiegeEngineTypesCount = new MBReadOnlyDictionary<SiegeEngineType, int>(reservedCounts);
    }

    private static void SetReadonlyField(SiegeEnginesContainer container, string fieldName, object value)
    {
        ReflectionUtils.SetPrivateField(typeof(SiegeEnginesContainer), fieldName, container, value);
    }
}
