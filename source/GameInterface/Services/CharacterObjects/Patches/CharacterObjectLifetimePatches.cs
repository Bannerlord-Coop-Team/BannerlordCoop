using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CharacterObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterObjects.Patches
{
    /// <summary>
    /// Lifetime Patches for CharacterObjects
    /// </summary>
    [HarmonyPatch]
    internal class CharacterObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<CharacterObjectLifetimePatches>();

        [HarmonyPatch(typeof(CharacterObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateCharacterObjectPrefix(ref CharacterObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CharacterObject), Environment.StackTrace);
                return false;
            }

            var message = new CharacterObjectCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }


    [HarmonyPatch]
    internal class MBObjectManagerLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<MBObjectManagerLifetimePatches>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateObject), new Type[] { typeof(string) }).MakeGenericMethod(typeof(CharacterObject));
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }

        //[HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CreateFromTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions;

            //var skipSetting = il.DefineLabel();

            //foreach (var instr in instructions)
            //{
            //    if (instr.Calls(AccessTools.PropertySetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId))))
            //    {
            //        yield return instr;
            //        //yield return new CodeInstruction(OpCodes.Pop);
            //        //yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId)));

            //        //// Compare current string id with null
            //        //// when the string id is not null we set it.
            //        //yield return new CodeInstruction(OpCodes.Ldnull);
            //        //yield return new CodeInstruction(OpCodes.Ceq);
            //        //yield return new CodeInstruction(OpCodes.Brfalse, skipSetting);

            //        //// when the string id is null we assume the game is creating it (not our mod)
            //        //yield return new CodeInstruction(OpCodes.Ldloc_0);
            //        //yield return new CodeInstruction(OpCodes.Box, typeof(CharacterObject));
            //        //yield return new CodeInstruction(OpCodes.Ldarg_1);
            //        //yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId)));

            //        //var nop = new CodeInstruction(OpCodes.Nop);
            //        //nop.labels.Add(skipSetting);
            //        //yield return nop;


            //        //yield return new CodeInstruction(OpCodes.Pop);
            //        //yield return new CodeInstruction(OpCodes.Pop);

            //        //yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MBObjectManagerLifetimePatches), nameof(SetStringIdIntercept)).MakeGenericMethod(typeof(CharacterObject)));

            //    }
            //    else
            //    {
            //        yield return instr;
            //    }
            //}
        }
    }
}
