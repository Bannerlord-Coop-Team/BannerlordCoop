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
using GameInterface.Utils.LocalEvents;

namespace GameInterface.Utils
{
    /// <summary>
    /// Allows for patches of collection types
    /// </summary>
    /// <typeparam name="TPatch">Class that is extending the <see cref="GenericPatches{TPatch, TInstance}"/> as its used for the interal Logger</typeparam>
    /// <typeparam name="TInstance">Class with the collections that should be patched</typeparam>
    public class GenericPatches<TPatch, TInstance> where TPatch : GenericPatches<TPatch, TInstance>
    {
        public static readonly ILogger Logger = LogManager.GetLogger<TPatch>();

        private static Dictionary<Type,Dictionary<string, FieldInfo>> fieldInfoCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static Dictionary<Type,Dictionary<string, PropertyInfo>> propertyInfoCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(TInstance));
        }

        #region ListTranspiler
        /// <summary>
        /// Used to transpile <see cref="List{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ListFieldChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        public static IEnumerable<CodeInstruction> ListFieldSetTranspiler<TItem, TSetMessage, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, List<TItem>>
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = ListFieldChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(instructions, fieldName);
            return FieldTranspiler<List<TItem>, TSetMessage>(patchedInstructions, fieldName);
        }

        /// <summary>
        /// Used to transpile <see cref="List{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListPropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ListPropertyChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsPropertyGetter(ci, propertyInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        public static IEnumerable<CodeInstruction> ListPropertySetTranspiler<TItem, TSetMessage, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TSetMessage : GenericEvent<TInstance, List<TItem>>
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = ListPropertyChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(instructions, propertyName);
            return PropertyTranspiler<List<TItem>, TSetMessage>(patchedInstructions, propertyName);
        }

        /// <summary>
        /// Intercept that gets called when an item is added from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
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
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
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
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> MBListFieldChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var addMethod = typeof(MBList<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(MBListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(MBList<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(MBListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        public static IEnumerable<CodeInstruction> MBListFieldSetTranspiler<TItem, TSetMessage, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, MBList<TItem>>
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = MBListFieldChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(instructions, fieldName);
            return FieldTranspiler<MBList<TItem>, TSetMessage>(patchedInstructions, fieldName);
        }

        /// <summary>
        /// Used to transpile <see cref="MBList{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TAddMessage">Message to be published on Add has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message to be published on Remove has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListPropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> MBListPropertyChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var addMethod = typeof(List<TItem>).GetMethod("Add");
            var addIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(MBListAddIntercept)).MakeGenericMethod(typeof(TItem), typeof(TAddMessage));
            var removeMethod = typeof(List<TItem>).GetMethod("Remove");
            var removeIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(MBListRemoveIntercept)).MakeGenericMethod(typeof(TItem), typeof(TRemoveMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsPropertyGetter(ci, propertyInfo),
                addMethod,
                addIntercept,
                removeMethod,
                removeIntercept);
        }

        /// <summary>
        /// Intercept that gets called when an item is added from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="items">The collection that gets changed</param>
        /// <param name="item">The item to be added</param>
        /// <param name="instance">The class that hold the collection</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void AddIntercept(MBList&lt;CharacterObject&gt; VolunteerTypes, CharacterObject value, Hero instance) <br/>
        /// => MBListAddIntercept&lt;CharacterObject, VolunteerTypesAdded&gt;(VolunteerTypes, value, instance);
        /// </remarks>
        public static IEnumerable<CodeInstruction> MBListPropertySetTranspiler<TItem, TSetMessage, TAddMessage, TRemoveMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, MBList<TItem>>
            where TAddMessage : GenericEvent<TInstance, TItem>
            where TRemoveMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = MBListPropertyChangeTranspiler<TItem, TAddMessage, TRemoveMessage>(instructions, fieldName);
            return PropertyTranspiler<MBList<TItem>, TSetMessage>(patchedInstructions, fieldName);
        }

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
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
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

        #region QueueTranspiler
        /// <summary>
        /// Used to transpile <see cref="Queue{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TEnqueueMessage">Message to be published on Enqueue has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TDequeueMessage">Message to be published on Dequeue has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueueFieldTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> QueueFieldChangeTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TEnqueueMessage : GenericEvent<TInstance, TItem>
            where TDequeueMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var enqueueMethod = typeof(Queue<TItem>).GetMethod("Enqueue");
            var enqueueIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(QueueEnqueueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TEnqueueMessage));
            var dequeueMethod = typeof(Queue<TItem>).GetMethod("Dequeue");
            var dequeueIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(QueueDequeueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TDequeueMessage));
            
            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                enqueueMethod,
                enqueueIntercept,
                dequeueMethod,
                dequeueIntercept);
        }

        public static IEnumerable<CodeInstruction> QueueFieldSetTranspiler<TItem, TSetMessage, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, Queue<TItem>>
            where TEnqueueMessage : GenericEvent<TInstance, TItem>
            where TDequeueMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = QueueFieldChangeTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(instructions, fieldName);
            return FieldTranspiler<Queue<TItem>, TSetMessage>(patchedInstructions, fieldName);
        }

        /// <summary>
        /// Used to transpile <see cref="Queue{TItem}"/>type Collections
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TEnqueueMessage">Message to be published on Enqueue has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <typeparam name="TDequeueMessage">Message to be published on Dequeue has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="propertyName">Name of the property to be patched</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueuePropertyTranspiler&lt;Alley, AlleyListUpdated, AlleyListRemoved&gt;(instructions);
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> QueuePropertyChangeTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TEnqueueMessage : GenericEvent<TInstance, TItem>
            where TDequeueMessage : GenericEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var enqueueMethod = typeof(Queue<TItem>).GetMethod("Enqueue");
            var enqueueIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(QueueEnqueueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TEnqueueMessage));
            var dequeueMethod = typeof(Queue<TItem>).GetMethod("Dequeue");
            var dequeueIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(QueueDequeueIntercept)).MakeGenericMethod(typeof(TItem), typeof(TDequeueMessage));

            return PatchInstructions(instructions.ToList(),
                (ci) => IsPropertyGetter(ci, propertyInfo),
                enqueueMethod,
                enqueueIntercept,
                dequeueMethod,
                dequeueIntercept);
        }

        public static IEnumerable<CodeInstruction> QueuePropertySetTranspiler<TItem, TSetMessage, TEnqueueMessage, TDequeueMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TSetMessage : GenericEvent<TInstance, TItem>
            where TEnqueueMessage : GenericEvent<TInstance, TItem>
            where TDequeueMessage : GenericEvent<TInstance, TItem>
        {
            var patchedInstructions = QueuePropertyChangeTranspiler<TItem, TEnqueueMessage, TDequeueMessage>(instructions, propertyName);
            return PropertyTranspiler<TItem, TSetMessage>(patchedInstructions, propertyName);
        }

        /// <summary>
        /// Intercept that gets called when an item is added from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
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
            where TMessage : GenericEvent<TInstance, TItem>
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                queue.Enqueue(item);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                queue.Enqueue(item);
                return;
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            queue.Enqueue(item);
        }

        /// <summary>
        /// Intercept that gets called when an item is removed from the collection
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericEvent{TInstance, TValue}"/></typeparam>
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
            where TMessage : GenericEvent<TInstance, TItem>
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return queue.Dequeue();
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return queue.Dequeue();
            }
            var item = queue.Dequeue();
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            return item;
        }
        #endregion

        #region ArrayTranspiler
        /// <summary>
        /// Used to transpile arrays
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericArrayChangedEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; AlleyTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ArrayTranspiler&lt;EquipmentElement, ItemSlotsArrayUpdated&gt;(instructions, nameof(Equipment._itemSlots));
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ArrayChangeTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var loadStack = new Stack<CodeInstruction>();
            var itemSlotArrayType = AccessTools.Field(typeof(TInstance), fieldName);
            var arrayAssignIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ArrayAssignIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));


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

        public static IEnumerable<CodeInstruction> ArrayPropertySetTranspiler<TItem, TSetMessage, TChangeMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TSetMessage : GenericEvent<TInstance, TItem[]>
            where TChangeMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var patchedInstructions = ArrayChangeTranspiler<TItem, TChangeMessage>(instructions, propertyName);
            return PropertyTranspiler<TItem[], TSetMessage>(patchedInstructions, propertyName);
        }

        public static IEnumerable<CodeInstruction> ArrayFieldSetTranspiler<TItem, TSetMessage, TChangeMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, TItem[]>
            where TChangeMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var patchedInstructions = ArrayChangeTranspiler<TItem, TChangeMessage>(instructions, fieldName);
            return FieldTranspiler<TItem[], TSetMessage>(patchedInstructions, fieldName);
        }

        /// <summary>
        /// Intercept that gets called on changes for transpiled arrays
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericArrayChangedEvent{TInstance, TValue}"/></typeparam>
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
            where TMessage : GenericArrayChangedEvent<TInstance, TItem>
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

        #region FieldTranspiler
        public static IEnumerable<CodeInstruction> FieldTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            AddToFieldCache(fieldInfo);
            var fieldIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(FieldIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(fieldInfo))
                {
                    yield return new CodeInstruction(OpCodes.Ldstr, fieldName);
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void FieldIntercept<TItem, TMessage>(TInstance instance, TItem item, string fieldName)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = fieldInfoCache[typeof(TInstance)][fieldName];

            var fieldValue = (TItem)fieldInfo.GetValue(instance);
            // Skip if value hasn´t changed
            if ((fieldValue == null && item == null) || item.Equals(fieldValue))
                return;

            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                fieldInfo.SetValue(instance, item);
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                fieldInfo.SetValue(instance, item);
                return;
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            fieldInfo.SetValue(instance, item);
        }

        private static void AddToFieldCache(FieldInfo fieldInfo)
        {
            if (!fieldInfoCache.ContainsKey(typeof(TInstance)))
                fieldInfoCache.Add(typeof(TInstance), new Dictionary<string, FieldInfo>());
            if (!fieldInfoCache[typeof(TInstance)].ContainsKey(fieldInfo.Name))
                fieldInfoCache[typeof(TInstance)].Add(fieldInfo.Name, fieldInfo);
        }
        #endregion

        #region PropertyTranspiler
        public static IEnumerable<CodeInstruction> PropertyTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            AddToPropertyCache(propertyInfo);
            var propertyIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(PropertyIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));

            foreach (var instruction in instructions)
            {
                if (IsPropertySetter(instruction, propertyInfo))
                {
                    yield return new CodeInstruction(OpCodes.Ldstr, propertyName);
                    yield return new CodeInstruction(OpCodes.Call, propertyIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void PropertyIntercept<TItem, TMessage>(TInstance instance, TItem item, string propertyName)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            var propertyInfo = propertyInfoCache[typeof(TInstance)][propertyName];

            var propertyValue = (TItem)propertyInfo.GetValue(instance);
            //// Skip if value hasn´t changed
            if ((propertyValue == null && item == null) || item.Equals(propertyValue))
                return;

            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                propertyInfo.SetValue(instance, item);
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                propertyInfo.SetValue(instance, item);
                return;
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            propertyInfo.SetValue(instance, item);
        }

        private static IEnumerable<CodeInstruction> PatchPropertySetter(List<CodeInstruction> instructionList, PropertyInfo propertyInfo, MethodInfo setIntercept)
        {
            var setters = instructionList.Where(pi => IsPropertySetter(pi, propertyInfo));
            // Method loads property
            foreach (var setterInst in setters)
            {
                var setterIndex = instructionList.IndexOf(setterInst);
                CodeInstruction getterInst = null;
                for (int i = setterIndex - 1; i >= 0; i--)
                {
                    var currentInst = instructionList[i];
                    if (IsPropertyGetter(currentInst, propertyInfo))
                    {
                        getterInst = currentInst;
                        break;
                    }
                }
                if (getterInst != null)
                {
                    List<CodeInstruction> loadStack = GetLoadStack(instructionList, instructionList.IndexOf(getterInst));
                    instructionList.InsertRange(setterIndex, loadStack);
                    setterIndex = instructionList.IndexOf(setterInst);
                    // Replace the Callvirt with the proper intercept
                    instructionList[setterIndex] = new CodeInstruction(OpCodes.Call,
                        setIntercept
                        );
                }
            }
            return instructionList;
        }


        private static void AddToPropertyCache(PropertyInfo propertyInfo)
        {
            if (!propertyInfoCache.ContainsKey(typeof(TInstance)))
                propertyInfoCache.Add(typeof(TInstance), new Dictionary<string, PropertyInfo>());
            if (!propertyInfoCache[typeof(TInstance)].ContainsKey(propertyInfo.Name))
                propertyInfoCache[typeof(TInstance)].Add(propertyInfo.Name, propertyInfo);
        }
        #endregion

        private static bool IsCorrectField(CodeInstruction codeInstruction, FieldInfo fieldInfo)
        {
            return codeInstruction.opcode == OpCodes.Ldfld && codeInstruction.operand as FieldInfo == fieldInfo;
        }

        private static bool IsPropertyGetter(CodeInstruction codeInstruction, PropertyInfo propertyInfo)
        {
            return (codeInstruction.opcode == OpCodes.Callvirt || codeInstruction.opcode == OpCodes.Call) && codeInstruction.operand as MethodInfo == propertyInfo.GetMethod;
        }

        private static bool IsPropertySetter(CodeInstruction codeInstruction, PropertyInfo propertyInfo)
        {
            return (codeInstruction.opcode == OpCodes.Callvirt || codeInstruction.opcode == OpCodes.Call) && codeInstruction.operand as MethodInfo == propertyInfo.SetMethod;
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
                    var stack = GetLoadStack(instructionList, targetIndex);
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

        private static List<CodeInstruction> GetLoadStack(List<CodeInstruction> instructionList, int targetIndex)
        {
            // Search beginning of loadStack to be able to retrieve the instance that the collection belongs to
            var stack = new List<CodeInstruction>();
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                var currentInst = instructionList[i];
                stack.Add(currentInst);
                if (IsLdArg(currentInst))
                    break;
            }
            stack.Reverse();
            return stack;
        }
    }
}
