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
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new BuildingCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);

            messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
