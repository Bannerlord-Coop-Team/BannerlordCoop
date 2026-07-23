using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;


namespace GameInterface.Services.Equipments.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Equipment"/> objects.
/// </summary>
[HarmonyPatch]
internal class EquipmentLifetimePatches

    
{
    private static readonly ILogger Logger = LogManager.GetLogger<EquipmentLifetimePatches>();

    // Equipment creation is handled by the auto-registry (see EquipmentRegistry.Constructors).
    // Only the Hero-driven removal lives here, because it removes both the battle and
    // civilian equipment together and so does not fit the per-instance DestroyMethods model.

    [HarmonyPatch(typeof(Hero), nameof(Hero.OnDeath))]
    [HarmonyPrefix]
    private static void OnDeathPrefix(ref Hero __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(Hero));
            return;
        }

        PublishEquipmentDestroyed(__instance);
    }


    [HarmonyPatch(typeof(Hero), nameof(Hero.ResetEquipments))]
    [HarmonyPrefix]
    private static void ResetEquipmentPrefix(ref Hero __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(Hero));
            return;
        }

        PublishEquipmentDestroyed(__instance);
    }

    private static void PublishEquipmentDestroyed(Hero hero)
    {
        MessageBroker.Instance.Publish(hero, new InstanceDestroyed<Equipment>(hero.BattleEquipment));
        MessageBroker.Instance.Publish(hero, new InstanceDestroyed<Equipment>(hero.CivilianEquipment));
        MessageBroker.Instance.Publish(hero, new InstanceDestroyed<Equipment>(hero.StealthEquipment));
    }
}
