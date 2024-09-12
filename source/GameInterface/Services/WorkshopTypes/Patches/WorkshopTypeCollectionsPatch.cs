using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using GameInterface.Services.WorkshopTypes.Messages;

namespace GameInterface.Services.WorkshopTypes.Patches
{
    [HarmonyPatch]
    internal class WorkshopTypeCollectionsPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<WorkshopTypeCollectionsPatch>();

        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(WorkshopType));
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var addMethod = typeof(List<WorkshopType.Production>).GetMethod("Add");
            var addIntercept = typeof(WorkshopTypeCollectionsPatch).GetMethod(nameof(AddIntercept));

            var removeMethod = typeof(List<WorkshopType.Production>).GetMethod("Remove");
            var removeIntercept = typeof(WorkshopTypeCollectionsPatch).GetMethod(nameof(RemoveIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == addMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, addIntercept);
                }
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == removeMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, removeIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void AddIntercept(List<WorkshopType.Production> _productions, WorkshopType.Production addProduction, WorkshopType instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                _productions.Add(addProduction);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                _productions.Add(addProduction);
                return;
            }

            var message = new ProductionsChanged(instance, addProduction, true);
            MessageBroker.Instance.Publish(instance, message);

            _productions.Add(addProduction);
        }

        public static bool RemoveIntercept(List<WorkshopType.Production> _productions, WorkshopType.Production removeProduction, WorkshopType instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return _productions.Remove(removeProduction);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return _productions.Remove(removeProduction);
            }

            var message = new ProductionsChanged(instance, removeProduction, false);
            MessageBroker.Instance.Publish(instance, message);

            return _productions.Remove(removeProduction);
        }
    }
}
