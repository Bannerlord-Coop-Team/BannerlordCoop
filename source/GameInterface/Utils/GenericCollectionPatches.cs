using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Serilog;
using Common.Logging;
using TaleWorlds.Library;

namespace GameInterface.Utils
{
    /// <summary>
    /// Allows for patches of collection types
    /// </summary>
    /// <typeparam name="TPatch">Class that is extending the <see cref="GenericCollectionPatches{TPatch, TInstance}"/> as its used for the interal Logger</typeparam>
    /// <typeparam name="TInstance">Class with the collections that should be patched</typeparam>
    internal class GenericCollectionPatches<TPatch, TInstance> where TPatch : GenericCollectionPatches<TPatch, TInstance>
    {
        public static readonly ILogger Logger = LogManager.GetLogger<TPatch>();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(TInstance));
        }

        #region ListTranspiler
        /// <summary>
        /// Used to transpile <see cref="List{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
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

        /// <summary>
        /// Intercept that gets called when an item is added from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="item">The item to be added</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void AddIntercept(List&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => ListAddIntercept&lt;CharacterObject, VolunteerTypesAdded&gt;(VolunteerTypes, value, instance);
        /// </remarks>
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

        /// <summary>
        /// Intercept that gets called when an item is removed from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="item">The item to be removed</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void RemoveIntercept(List&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => ListRemoveIntercept&lt;CharacterObject, VolunteerTypesRemoved&gt;(VolunteerTypes, value, instance);
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
        #endregion

        #region MBListTranspiler
        /// <summary>
        /// Used to transpile <see cref="MBList{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> MBListTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var addMethod = typeof(MBList<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(MBList<TItem>).GetMethod("Remove");
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

        /// <summary>
        /// Intercept that gets called when an item is added from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="item">The item to be added</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void AddIntercept(MBList&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => MBListAddIntercept&lt;CharacterObject, VolunteerTypesAdded&gt;(VolunteerTypes, value, instance);
        /// </remarks>
        public static void MBListAddIntercept<TItem, TMessage>(MBList<TItem> list, TItem item, TInstance instance)
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

        /// <summary>
        /// Intercept that gets called when an item is removed from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="item">The item to be removed</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void RemoveIntercept(MBList&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => MBListRemoveIntercept&lt;CharacterObject, VolunteerTypesRemoved&gt;(VolunteerTypes, value, instance);
        /// </remarks>
        public static bool MBListRemoveIntercept<TItem, TMessage>(MBList<TItem> items, TItem item, TInstance instance)
            where TMessage : IEvent
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return items.Remove(item);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return items.Remove(item);
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            return items.Remove(item);
        }
        #endregion

        #region ArrayTranspiler
        /// <summary>
        /// Used to transpile arrays
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericArrayEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ArrayTranspiler&lt;EquipmentElement, ItemSlotsArrayUpdated&gt;(instructions, nameof(Equipment._itemSlots));
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ArrayTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TMessage : GenericArrayEvent<TInstance, TItem>
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

        /// <summary>
        /// Intercept that gets called on changes for transpiled arrays
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericArrayEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="index">Index of the changed item</param>
        /// <param name="item">The item to be assigned</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void ArrayAssignIntercept(CharacterObject[] VolunteerTypes, int index, CharacterObject value, Hero instance) <br/>
        /// => ArrayAssignIntercept&lt;CharacterObject, VolunteerTypesArrayUpdated&gt;(VolunteerTypes, index, value, instance);
        /// </remarks>
        public static void ArrayAssignIntercept<TItem, TMessage>(TItem[] items, int index, TItem item, TInstance instance)
            where TMessage : GenericArrayEvent<TInstance, TItem>
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                items[index] = item;
                return;
            }

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(TInstance), Environment.StackTrace);
                return;
            }
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item, index);
            MessageBroker.Instance.Publish(instance, message);

            items[index] = item;
        }
        #endregion
    }
}
