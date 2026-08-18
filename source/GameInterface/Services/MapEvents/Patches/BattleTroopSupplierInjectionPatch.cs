using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Substitutes our server-driven <see cref="CoopTroopSupplier"/> for native per-side suppliers while a
/// coop battle's <see cref="BattleSpawnGate"/> is active. The optional NavalDLC spawn logic is patched
/// below by name so ordinary and non-naval battles keep their native suppliers.
/// </summary>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), MethodType.Constructor,
    new Type[] { typeof(IMissionTroopSupplier[]), typeof(BattleSideEnum), typeof(Mission.BattleSizeType) })]
internal class BattleTroopSupplierInjectionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleTroopSupplierInjectionPatch>();

    [HarmonyPrefix]
    private static void Prefix(IMissionTroopSupplier[] suppliers)
    {
        InstallCoopSuppliers(suppliers);
    }

    internal static void InstallCoopSuppliers(IMissionTroopSupplier[] suppliers)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;

        var mapEventId = BattleSpawnGate.ActiveMapEventId;
        if (mapEventId == null || suppliers == null) return;

        // Coop launchers may build their spawn logic with our suppliers already installed.
        if (suppliers.Length > 0 && suppliers[0] is CoopTroopSupplier) return;

        ContainerProvider.TryResolve<IObjectManager>(out var objectManager);
        ContainerProvider.TryResolve<IBattleAgentBudget>(out var agentBudget);

        // The array is indexed by BattleSideEnum (0 = Defender, 1 = Attacker).
        for (int i = 0; i < suppliers.Length; i++)
        {
            var supplier = new CoopTroopSupplier(mapEventId, (BattleSideEnum)i, objectManager, agentBudget);
            suppliers[i] = supplier;
            CoopTroopSupplierRegistry.Register(supplier);
            Logger.Information("[TroopSupply] Installed CoopTroopSupplier for {MapEvent} side {Side}", mapEventId, (BattleSideEnum)i);
        }
    }
}

/// <summary>
/// NavalDLC constructs its own agent spawn logic. Patch it by name so GameInterface remains loadable when
/// the optional DLC is absent, while replacing the native whole-side suppliers when it is present.
/// </summary>
[HarmonyPatch]
internal class NavalTroopSupplierInjectionPatch
{
    private const string NavalSpawnLogicTypeName =
        "NavalDLC.Missions.MissionLogics.DefaultNavalMissionAgentSpawnLogic";

    private static MethodBase targetMethod;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        var navalSpawnLogicType = AccessTools.TypeByName(NavalSpawnLogicTypeName);
        if (navalSpawnLogicType == null) return false;

        targetMethod = AccessTools.Constructor(navalSpawnLogicType, new[]
        {
            typeof(IMissionTroopSupplier[]),
            typeof(BattleSideEnum),
            typeof(int),
            typeof(int[]),
        });
        return targetMethod != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return targetMethod;
    }

    [HarmonyPrefix]
    private static void Prefix(IMissionTroopSupplier[] suppliers)
    {
        BattleTroopSupplierInjectionPatch.InstallCoopSuppliers(suppliers);
    }
}
