using Common.Messaging;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public readonly struct MangonelAmmoPickup : IEvent
{
    public Agent Agent { get; }
    public Mangonel Machine { get; }
    public MangonelAmmoPickup(Agent agent, Mangonel machine) { Agent = agent; Machine = machine; }
}

[HarmonyPatch(typeof(Mangonel), "OnTick")]
[HarmonyPatchCategory(MissionModule.WeaponPickupPatchCategory)]
public class MangonelAmmoPickupPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var equip = AccessTools.Method(typeof(Agent), nameof(Agent.EquipWeaponToExtraSlotAndWield));
        var consume = AccessTools.Method(typeof(RangedSiegeWeapon), "ConsumeAmmo");
        int pickups = 0;
        int consumes = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(equip))
            {
                var machine = new CodeInstruction(OpCodes.Ldarg_0);
                machine.MoveLabelsFrom(instruction);
                yield return machine;
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(MangonelAmmoPickupPatch), nameof(PickUp));
                pickups++;
            }
            else if (instruction.Calls(consume))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(MangonelAmmoPickupPatch), nameof(Consume));
                consumes++;
            }
            yield return instruction;
        }
        if (pickups != 1 || consumes != 1)
            throw new InvalidOperationException($"Failed to bind Mangonel pickup supply: {pickups} pickups, {consumes} consumes.");
    }

    private static bool UsesSharedSupply(RangedSiegeWeapon machine) =>
        BattleSpawnConfig.Enabled && BattleSpawnGate.IsCoopBattleActive && machine.StartingAmmoCount > 0;

    private static void PickUp(Agent agent, ref MissionWeapon weapon, Mangonel machine)
    {
        if (UsesSharedSupply(machine))
            MessageBroker.Instance.Publish(machine, new MangonelAmmoPickup(agent, machine));
        else
            agent.EquipWeaponToExtraSlotAndWield(ref weapon);
    }

    private static void Consume(RangedSiegeWeapon machine)
    {
        // Finite pickups are consumed once by the shared supply's host, before granting the item.
        if (!UsesSharedSupply(machine)) machine.ConsumeAmmo();
    }
}
