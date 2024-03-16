using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch]
    internal class HeroPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroPatches>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Hero));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LastTimeStampForActivityTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var lastTimeStampField = AccessTools.Field(typeof(Hero), nameof(Hero.LastTimeStampForActivity));
            var fieldIntercept = AccessTools.Method(typeof(HeroPatches), nameof(FieldIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == lastTimeStampField)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void FieldIntercept(int newTimestamp, Hero instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                instance.LastTimeStampForActivity = newTimestamp;
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                instance.LastTimeStampForActivity = newTimestamp;
                return;
            }

            MessageBroker.Instance.Publish(instance, new LastTimeStampChanged(newTimestamp, instance.StringId));

            instance.LastTimeStampForActivity = newTimestamp;
        }

    }
}
