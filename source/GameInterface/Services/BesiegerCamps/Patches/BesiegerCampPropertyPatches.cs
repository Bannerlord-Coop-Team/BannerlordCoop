using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
using TaleWorlds.Core;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json.Linq;
using System.Reflection;

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

        //[HarmonyPatch(nameof(BesiegerCamp.BattleSide), MethodType.Setter)]
        //[HarmonyPrefix]
        //private static bool SetBattleSidePrefix(BesiegerCamp __instance, BattleSideEnum value)
        //{
        //    return HandlePropertySet(__instance, nameof(BesiegerCamp.BattleSide), value);
        //}

        //[HarmonyPatch(nameof(BesiegerCamp.IsReadyToBesiege), MethodType.Setter)]
        //[HarmonyPrefix]
        //private static bool SetIsReadyToBesiegePrefix(BesiegerCamp __instance, bool value)
        //{
        //    return HandlePropertySet(__instance, nameof(BesiegerCamp.IsReadyToBesiege), value);
        //}

        [HarmonyPatch(nameof(BesiegerCamp.NumberOfTroopsKilledOnSide), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SetNumberOfTroopsKilledOnSidePrefix(BesiegerCamp __instance, int value)
        {
            return HandlePropertySet(__instance, nameof(BesiegerCamp.NumberOfTroopsKilledOnSide), value);
        }

        //private static bool TryGetTypeId<T>(T value, out string typeId)
        //{
        //    typeId = null;
        //    if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        //    {
        //        Logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
        //        return false;
        //    }

        //    if (typeof(T).IsClass)
        //    {
        //        if (!objectManager.TryGetId(value, out typeId))
        //        {
        //            Logger.Error("Unable to get ID for instance of type {type}", typeof(T).FullName);
        //            return false;
        //        }
        //        return true;
        //    }

        //    typeId = value.ToString(); // is this ok?
        //    return true;
        //}

        //string Serialize<T>(T obj) where T: struct
        //{

        //}

        //T Deserialize<T>(T obj) where T: struct
        //{

        //}

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

            //if (!TryGetTypeId<T>(value, out string typeId)) return false;

            var message = new BesiegerCampPropertyChanged(propInfo, instance, value);
            MessageBroker.Instance.Publish(instance, message);

            return ModInformation.IsServer;
        }
    }
}
