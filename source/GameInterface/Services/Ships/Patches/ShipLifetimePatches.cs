using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.Ships.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// Ship lifetime hooks the auto registry cannot express: the constructor state set after its registration prefix,
/// and the destroy, which happens in the static <see cref="DestroyShipAction"/>.
/// </summary>
[HarmonyPatch]
internal class ShipLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipLifetimePatches>();

    [HarmonyPatch(typeof(Ship), MethodType.Constructor, new[] { typeof(ShipHull) })]
    [HarmonyPostfix]
    private static void ConstructorPostfix(Ship __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // The registry's constructor prefix already reports a client create.
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new ShipInitialized(__instance));
    }

    // Postfix, so clients see the owner cleared and the destroy in that order.
    [HarmonyPatch(typeof(DestroyShipAction), nameof(DestroyShipAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void DestroyPostfix(Ship ship, bool __runOriginal)
    {
        // A blocking prefix (mission snapshots) means the ship was not destroyed.
        if (!__runOriginal) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed managed {name}", nameof(Ship));
            return;
        }

        MessageBroker.Instance.Publish(ship, new InstanceDestroyed<Ship>(ship));
    }
}
