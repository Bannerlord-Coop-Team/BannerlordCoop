using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Patches
{
    [HarmonyPatch(typeof(SiegeEngineConstructionProgress))]
    internal class SiegeEngineConstructionProgressPropertyPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineConstructionProgress>();

        [HarmonyPatch(nameof(SiegeEngineConstructionProgress.Progress), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetSiegeEngineConstructionProgressPrefix(SiegeEngineConstructionProgress __instance, SiegeEvent value)
        {
            return HandlePropertySet(__instance, nameof(SiegeEngineConstructionProgress.Progress), value);
        }

        private static bool HandlePropertySet<T>(SiegeEngineConstructionProgress instance, string propName, T value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var propInfo = typeof(SiegeEngineConstructionProgress).GetProperty(propName) ?? throw new ArgumentException("Invalid prop name!");

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", propInfo.Name, Environment.StackTrace);
                return false;
            }

            var message = new SiegeEngineConstructionProgressPropertyChanged(propInfo, instance, value);
            MessageBroker.Instance.Publish(instance, message);

            return ModInformation.IsServer;
        }
    }
}