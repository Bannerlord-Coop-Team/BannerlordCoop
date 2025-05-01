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
using System.Linq;
using Autofac.Core;

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
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ListFieldTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));


            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        /// <summary>
        /// Used to transpile <see cref="List{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListPropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ListPropertyTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectProperty(ci, propertyInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
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
            if (CallPolicy.IsOriginalAllowed())
            {
                list.Add(item);
                return;
            }

            // Skip method if called from client and allow origin
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);

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
            where TMessage : IEvent
        {
            var result = list.Remove(item);
            // Allows original method call if this thread is allowed
            if (CallPolicy.IsOriginalAllowed()) return result;

            // Skip method if called from client and allow origin
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return result;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);

            return result;
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
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> MBListFieldTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var addMethod = typeof(MBList<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(MBListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(MBList<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(MBListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));


            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        /// <summary>
        /// Used to transpile <see cref="MBList{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListPropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> MBListPropertyTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TAddMessage : GenericListEvent<TInstance, TItem>
            where TRemoveMessage : GenericListEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(MBListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(MBListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectProperty(ci, propertyInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
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
            list.Add(item);

            // Allows original method call if this thread is allowed
            if (CallPolicy.IsOriginalAllowed()) return;

            // Skip method if called from client and allow origin
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);
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
            var result = items.Remove(item);
            // Allows original method call if this thread is allowed
            if (CallPolicy.IsOriginalAllowed()) return result;

            // Skip method if called from client and allow origin
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return result;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);

            return result;
        }
        #endregion

        #region QueueTranspiler
        /// <summary>
        /// Used to transpile <see cref="Queue{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TEnqueueMessage">Message to be published on Enqueue has to extend <see cref="GenericQueueEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TDequeueMessage">Message to be published on Dequeue has to extend <see cref="GenericQueueEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueueFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> QueueFieldTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TEnqueueMessage : GenericQueueEvent<TInstance, TItem>
            where TDequeueMessage : GenericQueueEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var enqueueMethod = typeof(Queue<TItem>).GetMethod("Enqueue");
            var enqueueIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(QueueEnqueueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TEnqueueMessage));
            var dequeueMethod = typeof(Queue<TItem>).GetMethod("Dequeue");
            var dequeueIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(QueueDequeueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TDequeueMessage));
            
            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                enqueueMethod,
                enqueueIntercept,
                dequeueMethod,
                dequeueIntercept);
        }

        /// <summary>
        /// Used to transpile <see cref="Queue{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TEnqueueMessage">Message to be published on Enqueue has to extend <see cref="GenericQueueEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TDequeueMessage">Message to be published on Dequeue has to extend <see cref="GenericQueueEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueuePropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> QueuePropertyTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TEnqueueMessage : GenericQueueEvent<TInstance, TItem>
            where TDequeueMessage : GenericQueueEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var enqueueMethod = typeof(Queue<TItem>).GetMethod("Enqueue");
            var enqueueIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(QueueEnqueueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TEnqueueMessage));
            var dequeueMethod = typeof(Queue<TItem>).GetMethod("Dequeue");
            var dequeueIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(QueueDequeueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TDequeueMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectProperty(ci, propertyInfo),
                enqueueMethod,
                enqueueIntercept,
                dequeueMethod,
                dequeueIntercept);
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
        public static void QueueEnqueueIntercept<TItem, TMessage>(Queue<TItem> queue, TItem item, TInstance instance)
            where TMessage : GenericQueueEvent<TInstance, TItem>
        {
            queue.Enqueue(item);

            // Allows original method call if this thread is allowed
            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);
        }

        /// <summary>
        /// Intercept that gets called when an item is removed from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericListEvent{TInstance, TValue}"/></typeparam>
        /// <param name="queue">The collection that gets changed</param>
        /// <param name="item">The item to be removed</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void RemoveIntercept(MBList&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => MBListRemoveIntercept&lt;CharacterObject, VolunteerTypesRemoved&gt;(VolunteerTypes, value, instance);
        /// </remarks>
        public static TItem QueueDequeueIntercept<TItem, TMessage>(Queue<TItem> queue, TInstance instance)
            where TMessage : GenericQueueEvent<TInstance, TItem>
        {
            var item = queue.Dequeue();

            // Allows original method call if this thread is allowed
            if (CallPolicy.IsOriginalAllowed()) return item;

            // Skip method if called from client and allow origin
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return item;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);

            return item;
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
            var loadStack = new Stack<CodeInstruction>();
            var itemSlotArrayType = AccessTools.Field(typeof(TInstance), fieldName);
            var arrayAssignIntercept = typeof(GenericCollectionPatches<TPatch, TInstance>).GetMethod(nameof(ArrayAssignIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));


            foreach (var instruction in instructions)
            {
                if (loadStack.Count > 0 && instruction.opcode == OpCodes.Stelem_Ref)
                {
                    loadStack.Pop();

                    var newInstr = new CodeInstruction(OpCodes.Call, arrayAssignIntercept);
                    newInstr.labels = instruction.labels;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return newInstr;
                    continue;
                }

                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == itemSlotArrayType)
                {
                    loadStack.Push(instruction);
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
            items[index] = item;

            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item, index);
            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, message);
        }
        #endregion

        private static bool IsCorrectField(CodeInstruction codeInstruction, FieldInfo fieldInfo)
        {
            return codeInstruction.opcode == OpCodes.Ldfld && codeInstruction.operand as FieldInfo == fieldInfo;
        }

        private static bool IsCorrectProperty(CodeInstruction codeInstruction, PropertyInfo propertyInfo)
        {
            return (codeInstruction.opcode == OpCodes.Callvirt || codeInstruction.opcode == OpCodes.Call) && codeInstruction.operand as MethodInfo == propertyInfo.GetMethod;
        }

        private static bool IsLdArg(CodeInstruction instruction)
        {
            return instruction.opcode.Name.StartsWith(OpCodes.Ldarg.Name);
        }

        private static IEnumerable<CodeInstruction> PatchInstructions(List<CodeInstruction> instructionList, 
            Func<CodeInstruction, bool> targetLocator,
            MethodInfo addMethod, 
            MethodInfo addIntercept, 
            MethodInfo removeMethod, 
            MethodInfo removeIntercept
            )
        {
            var targetInstructions = instructionList.Where(ci => targetLocator(ci)).ToList();

            foreach (var targetInst in targetInstructions)
            {
                int targetIndex = instructionList.IndexOf(targetInst);

                // Check if we have enough instructions left so that there could be a call to the Add/Remove-Method
                // Usually there is an ldarg and then the callvirt
                if (instructionList.Count < targetIndex + 3)
                    return instructionList;

                // Check the next 2 instructions for the Callvirt
                var range = instructionList.GetRange(targetIndex + 1, 2);

                var changeInst = range.FirstOrDefault(inst => inst.opcode == OpCodes.Callvirt && (inst.operand as MethodInfo == addMethod || inst.operand as MethodInfo == removeMethod));
                // Add the instance that contains our target right before the Callvirt instruction
                if (changeInst != null)
                {
                    int changeIndex = instructionList.IndexOf(changeInst);
                    // Search beginning of loadStack to be able to retrieve the instance that the collection belongs to
                    var stack = new List<CodeInstruction>();
                    for(int i = targetIndex-1; i >=0; i--)
                    {
                        var currentInst = instructionList[i];
                        stack.Add(currentInst);
                        if (IsLdArg(currentInst))
                            break;
                    }
                    stack.Reverse();
                    //instructionList.InsertRange(changeIndex, instructionList.GetRange(targetIndex - 1, 1));
                    instructionList.InsertRange(changeIndex, stack);
                    changeIndex = instructionList.IndexOf(changeInst);
                    // Replace the Callvirt with the proper intercept
                    instructionList[changeIndex] = new CodeInstruction(OpCodes.Call,
                        changeInst.operand as MethodInfo == addMethod ? addIntercept : removeIntercept
                        );
                }
            }
            return instructionList;
        }
    }
}
