using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Patches
{
    public enum PropertyType
    {
        Capital,
        LastRunCampaignTime,
        WorkshopType,
        InitialCapital,
    }

    [HarmonyPatch(typeof(Workshop))]
    public class WorkshopPropertyPatches
    {
        private static ILogger Logger = LogManager.GetLogger<Workshop>();

        [HarmonyPatch(nameof(Workshop.Capital), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetCapitalPrefix(Workshop __instance, int value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", nameof(Workshop.Capital), Environment.StackTrace);
                return false;
            }

            var message = new WorkshopPropertyChanged(PropertyType.Capital, __instance, value.ToString());
            MessageBroker.Instance.Publish(__instance, message);

            return ModInformation.IsServer;
        }

        [HarmonyPatch(nameof(Workshop.LastRunCampaignTime), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetLastRunCampaignTimePrefix(Workshop __instance, CampaignTime value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", nameof(Workshop.LastRunCampaignTime), Environment.StackTrace);
                return false;
            }

            var message = new WorkshopPropertyChanged(PropertyType.LastRunCampaignTime, __instance, value.NumTicks.ToString());
            MessageBroker.Instance.Publish(__instance, message);

            return ModInformation.IsServer;
        }

        [HarmonyPatch(nameof(Workshop.WorkshopType), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetWorkshopTypePrefix(Workshop __instance, WorkshopType value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", nameof(Workshop.WorkshopType), Environment.StackTrace);
                return false;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
                return true;
            }

            objectManager.TryGetId(value, out string typeId);

            var message = new WorkshopPropertyChanged(PropertyType.WorkshopType, __instance, typeId);
            MessageBroker.Instance.Publish(__instance, message);

            return ModInformation.IsServer;
        }

        [HarmonyPatch(nameof(Workshop.InitialCapital), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetInitialCapitalPrefix(Workshop __instance, int value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", nameof(Workshop.InitialCapital), Environment.StackTrace);
                return false;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
                return true;
            }

            var message = new WorkshopPropertyChanged(PropertyType.InitialCapital, __instance, value.ToString());
            MessageBroker.Instance.Publish(__instance, message);

            return ModInformation.IsServer;
        }
    }
}
