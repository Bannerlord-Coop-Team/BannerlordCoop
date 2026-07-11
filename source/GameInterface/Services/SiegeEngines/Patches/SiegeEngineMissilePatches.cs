using Common;
using Common.Messaging;
using GameInterface.Services.SiegeEngines.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEngines.Patches;

/// <summary>
/// Replicates each bombardment missile the server's BombardTick fires. The client's map view renders whatever
/// is in the side's missile list, but its siege tick is server-gated so the list stays empty. Damage is already
/// authoritative through the engine hitpoints, so this carries only the visual projectile.
/// </summary>
[HarmonyPatch]
internal class SiegeEngineMissilePatches
{
    [HarmonyPatch(typeof(BesiegerCamp), nameof(BesiegerCamp.AddSiegeEngineMissile))]
    [HarmonyPostfix]
    private static void BesiegerCampAddMissilePostfix(BesiegerCamp __instance, SiegeEvent.SiegeEngineMissile missile)
    {
        Publish(__instance.SiegeEvent, BattleSideEnum.Attacker, missile);
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.AddSiegeEngineMissile))]
    [HarmonyPostfix]
    private static void SettlementAddMissilePostfix(Settlement __instance, SiegeEvent.SiegeEngineMissile missile)
    {
        Publish(__instance.SiegeEvent, BattleSideEnum.Defender, missile);
    }

    private static void Publish(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEvent.SiegeEngineMissile missile)
    {
        if (ModInformation.IsClient) return; // the client applies replicated missiles; don't echo them back
        if (siegeEvent == null || missile == null) return;

        MessageBroker.Instance.Publish(siegeEvent, new SiegeEngineMissileAdded(
            siegeEvent, side, missile.ShooterSiegeEngineType, missile.ShooterSlotIndex,
            missile.TargetType, missile.TargetSlotIndex, missile.TargetSiegeEngine,
            missile.CollisionTime.NumTicks, missile.FireDecisionTime.NumTicks, missile.HitSuccessful));
    }
}
