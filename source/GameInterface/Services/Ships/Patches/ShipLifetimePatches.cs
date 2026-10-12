using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Patches;

[HarmonyPatch]
internal class ShipLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipLifetimePatches>();

    [HarmonyPatch(typeof(DestroyShipAction), nameof(DestroyShipAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void Postfix_DestroyShipAction(Ship ship)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed managed {name}", typeof(Ship));
            return;
        }

        MessageBroker.Instance.Publish(ship, new InstanceDestroyed<Ship>(ship));
    }
}
