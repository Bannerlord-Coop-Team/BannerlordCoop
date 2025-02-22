using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Serilog;
using Common.Logging;

namespace GameInterface.Utils
{

    internal class GenericCollectionPatches<TPatch, TInstance> where TPatch : GenericCollectionPatches<TPatch, TInstance>
    {
        public static readonly ILogger Logger = LogManager.GetLogger<TPatch>();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(TInstance));
        }

        public static IEnumerable<CodeInstruction> ListTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

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
        public static void ListAddIntercept<TItem, TMessage>(List<TItem> list, TItem item, TInstance instance)
            where TMessage : IEvent
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                list.Add(item);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                list.Add(item);
                return;
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            list.Add(item);
        }

        public static bool ListRemoveIntercept<TItem, TMessage>(List<TItem> list, TItem item, TInstance instance)
            where TMessage: IEvent
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return list.Remove(item);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return list.Remove(item);
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            return list.Remove(item);
        }

        public static IEnumerable<CodeInstruction> ArrayTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
        {
            var stack = new Stack<CodeInstruction>();
            var itemSlotArrayType = AccessTools.Field(typeof(TInstance), fieldName);
            var arrayAssignIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ArrayAssignIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));


            foreach (var instruction in instructions)
            {
                if (stack.Count > 0 && instruction.opcode == OpCodes.Stelem_Ref)
                {
                    stack.Pop();

                    var newInstr = new CodeInstruction(OpCodes.Call, arrayAssignIntercept);
                    newInstr.labels = instruction.labels;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return newInstr;
                    continue;
                }

                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == itemSlotArrayType)
                {
                    stack.Push(instruction);
                }

                yield return instruction;
            }
        }

        public static void ArrayAssignIntercept<TItem, TMessage>(TItem[] _sides, int index, TItem value, TInstance instance)
            where TMessage : GenericArrayEvent<TInstance, TItem>
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                _sides[index] = value;
                return;
            }

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(TInstance), Environment.StackTrace);
                return;
            }
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, value, index);
            MessageBroker.Instance.Publish(instance, message);

            _sides[index] = value;
        }
    }
}
