//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Policies;
//using GameInterface.Services.CultureObjects.Messages;
//using HarmonyLib;
//using Serilog;
//using System;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.Core;

//namespace GameInterface.Services.BasicCharacterObjects.Patches
//{
//    [HarmonyPatch]
//    internal class BasicCultureObjectLifetimePatches
//    {
//        private static readonly ILogger Logger = LogManager.GetLogger<BasicCultureObjectLifetimePatches>();

//        [HarmonyPatch(typeof(BasicCultureObject), MethodType.Constructor)]
//        [HarmonyPrefix]
//        private static bool ctorPrefix(ref BasicCultureObject __instance)
//        {
//            // Call original if we call this function
//            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

//            if (ModInformation.IsClient)
//            {
//                Logger.Error("Client created unmanaged {name}\n"
//                    + "Callstack: {callstack}", typeof(CultureObject), Environment.StackTrace);

//                return false;
//            }

//            var message = new BasicCultureObjectCreated(__instance);

//            MessageBroker.Instance.Publish(null, message);

//            return true;
//        }
//    }
//}
