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
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch]
    internal class HeroFieldPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroFieldPatches>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Hero));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LastTimeStampForActivityTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var lastTimeStampField = AccessTools.Field(typeof(Hero), nameof(Hero.LastTimeStampForActivity));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(LastTimeStampForActivityIntercept));

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

        public static void LastTimeStampForActivityIntercept(int newTimestamp, Hero instance)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                instance.LastTimeStampForActivity = newTimestamp;
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                instance.LastTimeStampForActivity = newTimestamp;
                return;
            }

            MessageBroker.Instance.Publish(instance, new LastTimeStampChanged(newTimestamp, instance.StringId));

            instance.LastTimeStampForActivity = newTimestamp;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CharacterObjectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var characterObjectField = AccessTools.Field(typeof(Hero), nameof(Hero._characterObject));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(CharacterObjectIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == characterObjectField)
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

        public static void CharacterObjectIntercept(CharacterObject newCharacterObject, Hero instance)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                instance._characterObject = newCharacterObject;
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                instance._characterObject = newCharacterObject;
                return;
            }

            MessageBroker.Instance.Publish(instance, new CharacterObjectChanged(newCharacterObject.StringId, instance.StringId));

            instance._characterObject = newCharacterObject;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FirstNameTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var firstNameField = AccessTools.Field(typeof(Hero), nameof(Hero._firstName));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(FirstNameIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld && instruction.operand as FieldInfo == firstNameField)
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

        public static void FirstNameIntercept(TextObject newName, Hero instance)
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                instance._firstName = newName;
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                instance._firstName = newName;
                return;
            }

            MessageBroker.Instance.Publish(instance, new FirstNameChanged(newName.Value, instance.StringId));

            instance._firstName = newName;
        }
    }
}
