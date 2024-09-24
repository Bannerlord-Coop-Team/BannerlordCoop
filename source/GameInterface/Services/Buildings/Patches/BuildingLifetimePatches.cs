using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages.Lifetime;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Patches
{
    /// <summary>
    /// Lifetime Patches for Buildings
    /// </summary>
    [HarmonyPatch]
    internal class BuildingLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<BuildingLifetimePatches>();

        [HarmonyPatch(typeof(Building), MethodType.Constructor, typeof(BuildingType), typeof(Town), typeof(float), typeof(int))]
        [HarmonyPrefix]
        private static bool CreateBuildingPrefix(ref Building __instance, BuildingType buildingType, Town town, float buildingProgress, int currentLevel)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Building), Environment.StackTrace);
                return true;
            }

            var message = new BuildingCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
