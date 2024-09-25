using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.BesiegerCamps.Patches
{
    [HarmonyPatch(typeof(BesiegerCamp))]
    internal class BesiegerCampPropertyPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCamp>();

        [HarmonyPatch(nameof(BesiegerCamp.SiegeEvent), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetSiegeEventPrefix(BesiegerCamp __instance, SiegeEvent value)
        {
            return HandlePropertySet(__instance, nameof(BesiegerCamp.SiegeEvent), value);
        }

        [HarmonyPatch(nameof(BesiegerCamp.SiegeEngines), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetSiegeEnginesPrefix(BesiegerCamp __instance, SiegeEnginesContainer value)
        {
            return HandlePropertySet(__instance, nameof(BesiegerCamp.SiegeEngines), value);
        }

        [HarmonyPatch(nameof(BesiegerCamp.SiegeStrategy), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetSiegeStrategyPrefix(BesiegerCamp __instance, SiegeStrategy value)
        {
            return HandlePropertySet(__instance, nameof(BesiegerCamp.SiegeStrategy), value);
        }

        [HarmonyPatch(nameof(BesiegerCamp.NumberOfTroopsKilledOnSide), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetNumberOfTroopsKilledOnSidePrefix(BesiegerCamp __instance, int value)
        {
            return HandlePropertySet(__instance, nameof(BesiegerCamp.NumberOfTroopsKilledOnSide), value);
        }

        private static bool HandlePropertySet<T>(BesiegerCamp instance, string propName, T value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var propInfo = typeof(BesiegerCamp).GetProperty(propName) ?? throw new ArgumentException("Invalid prop name!");

            if (ModInformation.IsClient)
            {
                Logger.Error("Client tried to set {name}\n"
                    + "Callstack: {callstack}", propInfo.Name, Environment.StackTrace);
                return false;
            }

            var message = new BesiegerCampPropertyChanged(propInfo, instance, value);
            MessageBroker.Instance.Publish(instance, message);

            return ModInformation.IsServer;
        }
    }
}