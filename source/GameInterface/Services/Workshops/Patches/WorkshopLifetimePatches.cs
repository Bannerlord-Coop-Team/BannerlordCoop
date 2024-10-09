using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Workshops.Data;
using GameInterface.Services.Workshops.Messages;

namespace GameInterface.Services.Workshops.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Workshop"/> objects.
/// </summary>
[HarmonyPatch]
internal class WorkshopLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<WorkshopLifetimePatches>();


    [HarmonyPatch(typeof(Workshop), MethodType.Constructor, typeof(Settlement), typeof(String))]
    [HarmonyPrefix]
    private static bool CreateWorkshopPrefix(ref Workshop __instance, Settlement settlement, String tag)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Workshop), Environment.StackTrace);


            return true;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            objectManager.AddNewObject(__instance, out var newWorkshopId);

            var data = new WorkshopCreatedData(newWorkshopId, settlement.StringId, tag);
            var message = new WorkshopCreated(data);

            MessageBroker.Instance.Publish(null, message);
        }

        return true;
    }
}
