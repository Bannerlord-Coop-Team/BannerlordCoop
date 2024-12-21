using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.WeaponDesigns.Messages.Collection;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch]
    internal class CraftingCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCollectionPatches>();

        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(Crafting));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CraftingTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var listAddMethod = AccessTools.Method(typeof(List<WeaponDesign>), "Add");
            var listAddOverrideMethod = AccessTools.Method(typeof(CraftingCollectionPatches), nameof(ListAddOverride));

            var listRemoveMethod = AccessTools.Method(typeof(List<WeaponDesign>), "Remove");
            var listRemoveOverrideMethod = AccessTools.Method(typeof(CraftingCollectionPatches), nameof(ListRemoveOverride));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listAddMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, listAddOverrideMethod);
                }
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listRemoveMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, listRemoveOverrideMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void ListAddOverride(List<WeaponDesign> _history, WeaponDesign weaponDesign, Crafting instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                _history.Add(weaponDesign);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                _history.Add(weaponDesign);
                return;
            }

            MessageBroker.Instance.Publish(instance, new WeaponDesignAdded(instance, weaponDesign));

            _history.Add(weaponDesign);
        }

        public static bool ListRemoveOverride(List<WeaponDesign> _history, WeaponDesign weaponDesign, Crafting instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return _history.Remove(weaponDesign);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return _history.Remove(weaponDesign);
            }

            MessageBroker.Instance.Publish(instance, new WeaponDesignRemoved(instance, weaponDesign));

            return _history.Remove(weaponDesign);
        }
    }
}