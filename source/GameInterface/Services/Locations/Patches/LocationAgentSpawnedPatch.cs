using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// [Host] Captures every non-player agent spawned in the active settlement location mission (SR-021)
/// so the location replicator can register + replicate it. Gated on the location gate's CONFIRMED
/// local host — before the assignment arrives nothing spawns natively anyway
/// (<see cref="LocationNativeSpawnSuppressionPatches"/>), and puppet application brackets itself with
/// <see cref="LocationNpcGate.SuppressCapture"/>. Coexists with the battle capture patch on the same
/// method: its <c>IsCoopBattleActive</c> gate is false in a settlement, and this gate is false in a
/// battle.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool) })]
internal class LocationAgentSpawnedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __result)
    {
        if (!LocationNpcGate.IsCoopLocationMissionActive) return;
        if (!LocationNpcGate.IsLocalHostConfirmed) return;
        if (LocationNpcGate.SuppressCapture) return;
        if (__result == null) return;

        // The player's own agent goes through Mission.SpawnAgent too (MissionHelper.SpawnPlayer) —
        // players replicate via the join-info path, never as NPC records.
        if (__result.Controller == AgentControllerType.Player) return;

        // Settlement humans always spawn on foot (native passes NoHorses everywhere), so a mount here
        // is unexpected; animals arrive via SpawnMonster and are captured by LocationMonsterSpawnedPatch.
        if (__result.IsMount) return;

        MessageBroker.Instance.Publish(__result, new AgentSpawnedInLocation(__result));
    }
}

/// <summary>
/// [Host] Captures settlement animals (SR-020/V3): sheep/cows/hogs/geese/chickens and scene horses
/// spawn via <c>Mission.SpawnMonster</c>, which builds the agent through
/// <c>CreateHorseAgentFromRosterElements</c> and never passes the <c>SpawnAgent(AgentBuildData,
/// bool)</c> overload the human capture patches. The item identities ride along so the receiver can
/// re-spawn the same monster.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnMonster),
    new[] { typeof(EquipmentElement), typeof(EquipmentElement), typeof(Vec3), typeof(Vec2), typeof(int) },
    new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal })]
internal class LocationMonsterSpawnedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __result, EquipmentElement equipmentElement, EquipmentElement harnessRosterElement)
    {
        if (!LocationNpcGate.IsCoopLocationMissionActive) return;
        if (!LocationNpcGate.IsLocalHostConfirmed) return;
        if (LocationNpcGate.SuppressCapture) return;
        if (__result == null) return;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;

        string itemId = null;
        if (equipmentElement.Item != null)
            objectManager.TryGetId(equipmentElement.Item, out itemId);

        string harnessItemId = null;
        if (harnessRosterElement.Item != null)
            objectManager.TryGetId(harnessRosterElement.Item, out harnessItemId);

        if (itemId == null)
        {
            // Without the item the receiver cannot re-spawn the monster; keep it host-local rather
            // than ship a broken record.
            return;
        }

        MessageBroker.Instance.Publish(__result, new MonsterSpawnedInLocation(__result, itemId, harnessItemId));
    }
}
