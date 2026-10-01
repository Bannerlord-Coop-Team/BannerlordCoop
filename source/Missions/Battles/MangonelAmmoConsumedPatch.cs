using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public readonly struct MangonelLoadTick : IEvent
{
    public Mangonel Machine { get; }
    public MangonelLoadTick(Mangonel machine) { Machine = machine; }
}

public readonly struct MangonelAmmoConsumed : IEvent
{
    public Mangonel Machine { get; }
    public Agent Agent { get; }

    public MangonelAmmoConsumed(Mangonel machine, Agent agent)
    {
        Machine = machine;
        Agent = agent;
    }
}

[HarmonyPatch(typeof(Mangonel), "OnTick")]
[HarmonyPatchCategory(MissionModule.WeaponPickupPatchCategory)]
public class MangonelAmmoConsumedPatch
{
    private static void Prefix(Mangonel __instance, out Agent __state)
    {
        __state = null;
        if (AllowedThread.IsThisThreadAllowed() ||
            !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(__instance.Id.Id) ||
            __instance.State != RangedSiegeWeapon.WeaponState.LoadingAmmo) return;
        MessageBroker.Instance.Publish(__instance, new MangonelLoadTick(__instance));
        Agent user = __instance.LoadAmmoStandingPoint?.UserAgent;
        if (user != null && user.GetPrimaryWieldedItemIndex() == EquipmentIndex.ExtraWeaponSlot &&
            !user.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty &&
            ReferenceEquals(user.Equipment[EquipmentIndex.ExtraWeaponSlot].Item, __instance.OriginalMissileItem))
            __state = user;
    }

    private static void Postfix(Mangonel __instance, Agent __state)
    {
        if (__state == null || __instance.State != RangedSiegeWeapon.WeaponState.WaitingBeforeIdle ||
            !__state.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return;
        MessageBroker.Instance.Publish(__instance, new MangonelAmmoConsumed(__instance, __state));
    }
}
