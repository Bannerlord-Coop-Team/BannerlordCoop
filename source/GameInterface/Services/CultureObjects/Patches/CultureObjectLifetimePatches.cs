using Autofac;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CultureObjects.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects.Patches
{
    [HarmonyPatch]
    internal class CultureObjectLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CultureObjectLifetimePatches>();

        [HarmonyPatch(typeof(CultureObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool ctorPrefix(ref CultureObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CultureObject), Environment.StackTrace);

                return false;
            }

            var message = new CultureObjectCreated(__instance);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }

    //[HarmonyPatch]
    //public class MBObjectManagerLifetimePatches
    //{
    //    private static ILogger Logger = LogManager.GetLogger<MBObjectManagerLifetimePatches>();

    //    private static IEnumerable<MethodBase> TargetMethods()
    //    {
    //        yield return AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateObject), new Type[] { typeof(string) }).MakeGenericMethod(typeof(CultureObject));
    //        yield return AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateObject), new Type[] { typeof(string) }).MakeGenericMethod(typeof(CharacterObject));
    //    }

    //    public static void Intercept(MBObjectBase mBObject)
    //    {
    //        if (mBObject.StringId == null)
    //        {
    //            mBObject.StringId = mBObject.GetType().Name.ToString() + "_1";
    //        }
    //    }

    //    [HarmonyTranspiler]
    //    private static IEnumerable<CodeInstruction> CreateFromTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    //    {
    //        bool singleShot = true;
    //        bool singleShot2 = true;

    //        //var skipSetting = il.DefineLabel();

    //        foreach (var instr in instructions)
    //        {
    //            if (instr.opcode == OpCodes.Stloc_0)
    //            {
    //                yield return instr;

    //                //if (!singleShot) continue;

    //                //singleShot = false;

    //                //yield return new CodeInstruction(OpCodes.Ldloc_0);
    //                //yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId)));
    //                //yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(string), nameof(string.IsNullOrEmpty)));
    //                //yield return new CodeInstruction(OpCodes.Brtrue, skipSetting);
    //            }
    //            else if (instr.opcode == OpCodes.Ldarg_0)
    //            {
    //                //instr.labels.Add(skipSetting);

    //                yield return instr;

    //                //if (!singleShot2) continue;

    //                //singleShot2 = false;
    //            }
    //            else
    //            {
    //                yield return instr;
    //            }
    //        }
    //    }
    //}
}
