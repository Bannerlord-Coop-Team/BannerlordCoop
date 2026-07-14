using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Substitutes our server-driven <see cref="CoopTroopSupplier"/> for the native per-side supplier in a coop
/// field battle. The campaign builds the native suppliers in <c>CampaignMission.OpenBattleMission</c> and
/// passes the array to this constructor; replacing the array's entries here (before the constructor body
/// reads them) means the native deployment/reinforcement logic drives our supplier instead, with no other
/// changes. Only active in a coop battle (see <see cref="BattleSpawnGate"/>); ordinary battles keep their
/// native suppliers.
/// </summary>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), MethodType.Constructor,
    new Type[] { typeof(IMissionTroopSupplier[]), typeof(BattleSideEnum), typeof(Mission.BattleSizeType) })]
internal class BattleTroopSupplierInjectionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleTroopSupplierInjectionPatch>();

    [HarmonyPrefix]
    private static void Prefix(IMissionTroopSupplier[] suppliers)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;

        var mapEventId = BattleSpawnGate.ActiveMapEventId;
        if (mapEventId == null || suppliers == null) return;

        // The coop field-battle launcher (CoopFieldBattleLauncher) now builds the spawn logic with our
        // suppliers already installed, so there is nothing to substitute — skip. This patch only still matters
        // for any spawn logic the native path constructs while a coop battle is active.
        if (suppliers.Length > 0 && suppliers[0] is CoopTroopSupplier) return;

        // SOAK-LOG PHASE of retiring this native substitution path (RANK 12 — planned demotion; see
        // BattleMissionEntryPatch). The coop launchers install CoopTroopSuppliers themselves, so reaching this
        // substitution means a native OpenBattleMission constructed the spawn logic while a coop battle is active
        // — the very path slated to be blocked. Log loudly (no behavior change) so the soak proves whether this
        // ever happens in coop play before the demotion to a blocking guard lands.
        Logger.Warning(
            "[TroopSupply][SOAK] Substituting CoopTroopSuppliers for native suppliers during an active coop battle (mapEvent={MapEvent}). " +
            "This native substitution path is slated for demotion to a blocking guard.",
            mapEventId);

        // No DI here (static patch), so resolve the object manager ONCE and hand it to the suppliers (they no
        // longer hit the service locator per agent on the supply path).
        ContainerProvider.TryResolve<IObjectManager>(out var objectManager);

        // The array is indexed by BattleSideEnum (0 = Defender, 1 = Attacker), matching how the campaign and
        // the constructor build/consume it.
        for (int i = 0; i < suppliers.Length; i++)
        {
            var supplier = new CoopTroopSupplier(mapEventId, (BattleSideEnum)i, objectManager);
            suppliers[i] = supplier;
            CoopTroopSupplierRegistry.Register(supplier);
            Logger.Information("[TroopSupply] Installed CoopTroopSupplier for {MapEvent} side {Side}", mapEventId, (BattleSideEnum)i);
        }
    }
}
