using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Utils.LocalEvents;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

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

        private static Dictionary<Type, Dictionary<string, FieldInfo>> fieldInfoCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static Dictionary<Type, Dictionary<string, PropertyInfo>> propertyInfoCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListFieldTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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
            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(fieldInfo, addIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(fieldInfo, removeIntercept);
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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ListPropertyTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(propertyInfo, addIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(propertyInfo, removeIntercept);

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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListFieldTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(fieldInfo, addIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(fieldInfo, removeIntercept);

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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => MBListPropertyTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(propertyInfo, addIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(propertyInfo, removeIntercept);

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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueueFieldTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(fieldInfo, enqueueIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(fieldInfo, dequeueIntercept);

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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => QueuePropertyTranspiler&lt;Workshop, WorkshopListUpdated, WorkshopListRemoved&gt;(instructions);
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

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(propertyInfo, enqueueIntercept);
            GenericPatchHelpers.CollectionRemoveInterceptCache.TryAdd(propertyInfo, dequeueIntercept);
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

        public static IEnumerable<CodeInstruction> QueueClearTranspiler<TItem>(IEnumerable<CodeInstruction> instructions)
        {
            var clearMethod = typeof(Queue<TItem>).GetMethod(nameof(Queue<TItem>.Clear));
            var clearIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(QueueClearIntercept)).MakeGenericMethod();

            foreach ( var instruction in instructions )
            {
                if (instruction.Calls(clearMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, clearIntercept);
                }

                yield return instruction;
            }
        }

        public static void QueueClearIntercept<TItem>(Queue<TItem> queue)
        {
            // message
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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
                return queue.Dequeue();
            }
            var item = queue.Dequeue();
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item);
            MessageBroker.Instance.Publish(instance, message);

            return item;
        }
        #endregion

        #region DictionaryTranspiler
        /// <summary>
        /// Used to transpile <see cref="Dictionary{TKey, TValue}"/> type collections. Intercepts every
        /// mutation method available to game code (net472 surface): Add, the indexer setter (set_Item),
        /// Remove(key) and Clear.
        /// </summary>
        /// <typeparam name="TKey">Type of the dictionary keys</typeparam>
        /// <typeparam name="TValue">Type of the dictionary values</typeparam>
        /// <typeparam name="TUpsertMessage">Message published on Add and on indexer set (both apply as an
        /// upsert on the receiving side), has to extend <see cref="GenericPairEvent{TInstance, TKey, TValue}"/></typeparam>
        /// <typeparam name="TRemoveMessage">Message published on Remove, has to extend <see cref="GenericEvent{TInstance, TKey}"/></typeparam>
        /// <typeparam name="TClearMessage">Message published on Clear, needs a (TInstance) constructor</typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <param name="fieldName">Name of the field to be patched</param>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> DictionaryFieldChangeTranspiler<TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TUpsertMessage : GenericPairEvent<TInstance, TKey, TValue>
            where TRemoveMessage : GenericEvent<TInstance, TKey>
            where TClearMessage : IEvent
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            return DictionaryChangeTranspiler<TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage>(
                instructions, fieldInfo, (ci) => IsCorrectField(ci, fieldInfo));
        }

        /// <summary>
        /// Used to transpile <see cref="Dictionary{TKey, TValue}"/> type collections accessed through a property.
        /// See <see cref="DictionaryFieldChangeTranspiler{TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage}"/>.
        /// </summary>
        public static IEnumerable<CodeInstruction> DictionaryPropertyChangeTranspiler<TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TUpsertMessage : GenericPairEvent<TInstance, TKey, TValue>
            where TRemoveMessage : GenericEvent<TInstance, TKey>
            where TClearMessage : IEvent
        {
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            return DictionaryChangeTranspiler<TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage>(
                instructions, propertyInfo, (ci) => IsPropertyGetter(ci, propertyInfo));
        }

        private static IEnumerable<CodeInstruction> DictionaryChangeTranspiler<TKey, TValue, TUpsertMessage, TRemoveMessage, TClearMessage>(
            IEnumerable<CodeInstruction> instructions, MemberInfo memberInfo, Func<CodeInstruction, bool> loadsMember)
            where TUpsertMessage : GenericPairEvent<TInstance, TKey, TValue>
            where TRemoveMessage : GenericEvent<TInstance, TKey>
            where TClearMessage : IEvent
        {
            var patchesType = typeof(GenericPatches<TPatch, TInstance>);
            var addIntercept = patchesType.GetMethod(nameof(DictionaryAddIntercept)).MakeGenericMethod(typeof(TKey), typeof(TValue), typeof(TUpsertMessage));
            var setItemIntercept = patchesType.GetMethod(nameof(DictionarySetItemIntercept)).MakeGenericMethod(typeof(TKey), typeof(TValue), typeof(TUpsertMessage));
            var removeIntercept = patchesType.GetMethod(nameof(DictionaryRemoveIntercept)).MakeGenericMethod(typeof(TKey), typeof(TValue), typeof(TRemoveMessage));
            var clearIntercept = patchesType.GetMethod(nameof(DictionaryClearIntercept)).MakeGenericMethod(typeof(TKey), typeof(TValue), typeof(TClearMessage));

            GenericPatchHelpers.DictionaryAddInterceptCache.TryAdd(memberInfo, addIntercept);
            GenericPatchHelpers.DictionarySetItemInterceptCache.TryAdd(memberInfo, setItemIntercept);
            GenericPatchHelpers.DictionaryRemoveInterceptCache.TryAdd(memberInfo, removeIntercept);
            GenericPatchHelpers.DictionaryClearInterceptCache.TryAdd(memberInfo, clearIntercept);

            var dictType = typeof(Dictionary<TKey, TValue>);
            var interceptByMethod = new Dictionary<MethodInfo, MethodInfo>
            {
                [dictType.GetMethod(nameof(Dictionary<TKey, TValue>.Add))] = addIntercept,
                [dictType.GetProperty("Item").SetMethod] = setItemIntercept,
                [dictType.GetMethod(nameof(Dictionary<TKey, TValue>.Remove), new[] { typeof(TKey) })] = removeIntercept,
                [dictType.GetMethod(nameof(Dictionary<TKey, TValue>.Clear))] = clearIntercept,
            };

            var codes = new List<CodeInstruction>(instructions);

            // First pass: for every load of the synced member, walk forward simulating relative stack
            // depth to find the call that consumes the loaded dictionary. A member load can sit several
            // instructions before its call (Add/set_Item push key AND value in between), so a fixed
            // lookahead window cannot match here. The dictionary is the receiver of a matched call
            // exactly when that call pops one slot deeper than everything pushed since the load.
            //
            // Example - TownMarketData.SetItemData compiles 'this._itemDict[itemCategory] = itemData' to:
            //   ldarg.0                                     owner for the ldfld below
            //   ldfld _itemDict      -> depth 0             the tracked member load
            //   ldarg.1 (key)        -> depth 1
            //   ldarg.2 (value)      -> depth 2
            //   callvirt set_Item       pops 3 == depth + 1 -> the dictionary is the receiver: match
            var matches = new List<(int loadIndex, int callIndex, MethodInfo intercept)>();

            for (int i = 0; i < codes.Count; i++)
            {
                if (!loadsMember(codes[i])) continue;

                int depth = 0; // number of stack slots above the loaded dictionary
                for (int j = i + 1; j < codes.Count; j++)
                {
                    var inst = codes[j];

                    // Branching (or being a branch target) makes linear stack tracking unsound - skip this load
                    if (IsControlFlow(inst) || inst.labels.Count > 0) break;

                    int pops = GetPopCount(inst);
                    int pushes = GetPushCount(inst);

                    if (pops > depth)
                    {
                        // This instruction consumes the dictionary itself. It is our pattern only when
                        // it is a call to a tracked mutation method with the dictionary as receiver
                        // (the deepest popped slot); anything else (stloc, pop, passed as argument to
                        // another method, ...) aliases or consumes the dictionary in a way we cannot
                        // track, so leave the code untouched.
                        if ((inst.opcode == OpCodes.Callvirt || inst.opcode == OpCodes.Call) &&
                            inst.operand is MethodInfo target &&
                            interceptByMethod.TryGetValue(target, out var intercept) &&
                            pops == depth + 1)
                        {
                            matches.Add((i, j, intercept));
                        }

                        break;
                    }

                    depth += pushes - pops;
                }
            }

            // Second pass: for each match, duplicate the owner reference (it is on the stack right below
            // the dictionary at the member load) so it rides under the call arguments until the intercept
            // consumes it: [owner] -> dup -> [owner, owner] -> member load -> [owner, dict] -> args ->
            // [owner, dict, key, value] -> call intercept(instance, dict, key, value). Branches that
            // targeted the member load must land on the dup so the owner is still duplicated.
            var memberLoadsToPatch = new HashSet<int>(matches.Select(m => m.loadIndex));
            var interceptByCallIndex = matches.ToDictionary(m => m.callIndex, m => m.intercept);

            for (int i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];

                if (memberLoadsToPatch.Contains(i))
                {
                    yield return new CodeInstruction(OpCodes.Dup)
                    {
                        labels = instruction.labels.ToList(),
                        blocks = instruction.blocks.ToList(),
                    };

                    instruction.labels.Clear();
                    instruction.blocks.Clear();
                    yield return instruction;
                    continue;
                }

                if (interceptByCallIndex.TryGetValue(i, out var intercept))
                {
                    yield return new CodeInstruction(OpCodes.Call, intercept)
                    {
                        labels = instruction.labels.ToList(),
                        blocks = instruction.blocks.ToList(),
                    };
                    continue;
                }

                yield return instruction;
            }
        }

        private static bool IsControlFlow(CodeInstruction instruction)
        {
            switch (instruction.opcode.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Cond_Branch:
                case FlowControl.Return:
                case FlowControl.Throw:
                case FlowControl.Break:
                    return true;
                default:
                    return false;
            }
        }

        private static int GetPushCount(CodeInstruction instruction)
        {
            switch (instruction.opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;
                case StackBehaviour.Push1_push1:
                    return 2;
                case StackBehaviour.Varpush:
                    // call/callvirt: pushes the return value unless void
                    if (instruction.operand is MethodInfo method)
                        return method.ReturnType == typeof(void) ? 0 : 1;
                    return 1;
                default:
                    return 0;
            }
        }

        // Pop count for call-shaped instructions whose operand cannot be resolved (calli, ...). Larger
        // than any real evaluation stack, so the matcher's 'pops > depth' consumption check always fires
        // and the scan safely gives up on that member load.
        private const int UnknownPopCount = int.MaxValue / 2;

        private static int GetPopCount(CodeInstruction instruction)
        {
            switch (instruction.opcode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                case StackBehaviour.Popref_popi_pop1:
                    return 3;
                case StackBehaviour.Varpop:
                    // call/callvirt/newobj: pops the arguments, plus the receiver for instance calls
                    if (instruction.operand is MethodBase methodBase)
                    {
                        int count = methodBase.GetParameters().Length;
                        if (instruction.opcode != OpCodes.Newobj && !methodBase.IsStatic)
                            count++;
                        return count;
                    }
                    return UnknownPopCount;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Intercept that gets called when a pair is added to the dictionary via Add. Publishes the
        /// same upsert message as <see cref="DictionarySetItemIntercept{TKey, TValue, TMessage}"/> -
        /// the receiving side applies both operations identically (indexer assignment).
        /// </summary>
        /// <remarks>
        /// The instance comes first because the transpiler duplicates the owner under the
        /// dictionary on the evaluation stack (see the transpiler comment).
        /// </remarks>
        public static void DictionaryAddIntercept<TKey, TValue, TMessage>(TInstance instance, Dictionary<TKey, TValue> dict, TKey key, TValue value)
            where TMessage : GenericPairEvent<TInstance, TKey, TValue>
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                dict.Add(key, value);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(Dictionary<TKey, TValue>));
                dict.Add(key, value);
                return;
            }

            // Mutate first so a duplicate-key exception propagates without a phantom broadcast
            dict.Add(key, value);

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, key, value);
            MessageBroker.Instance.Publish(instance, message);
        }

        /// <summary>
        /// Intercept that gets called when a pair is assigned through the dictionary indexer. Publishes
        /// the same upsert message as <see cref="DictionaryAddIntercept{TKey, TValue, TMessage}"/>.
        /// </summary>
        public static void DictionarySetItemIntercept<TKey, TValue, TMessage>(TInstance instance, Dictionary<TKey, TValue> dict, TKey key, TValue value)
            where TMessage : GenericPairEvent<TInstance, TKey, TValue>
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                dict[key] = value;
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(Dictionary<TKey, TValue>));
                dict[key] = value;
                return;
            }

            // Value-type dictionary entries represent an absolute value. Reassigning an identical
            // value cannot change the receiver, so avoid publishing a redundant network update.
            bool unchanged = typeof(TValue).IsValueType &&
                dict.TryGetValue(key, out TValue existingValue) &&
                EqualityComparer<TValue>.Default.Equals(existingValue, value);

            dict[key] = value;

            if (unchanged) return;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, key, value);
            MessageBroker.Instance.Publish(instance, message);
        }

        /// <summary>
        /// Intercept that gets called when a key is removed from the dictionary
        /// </summary>
        public static bool DictionaryRemoveIntercept<TKey, TValue, TMessage>(TInstance instance, Dictionary<TKey, TValue> dict, TKey key)
            where TMessage : GenericEvent<TInstance, TKey>
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return dict.Remove(key);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(Dictionary<TKey, TValue>));
                return dict.Remove(key);
            }

            // Only broadcast removes that actually removed something
            if (!dict.Remove(key)) return false;

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, key);
            MessageBroker.Instance.Publish(instance, message);

            return true;
        }

        /// <summary>
        /// Intercept that gets called when the dictionary is cleared
        /// </summary>
        public static void DictionaryClearIntercept<TKey, TValue, TMessage>(TInstance instance, Dictionary<TKey, TValue> dict)
            where TMessage : IEvent
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                dict.Clear();
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(Dictionary<TKey, TValue>));
                dict.Clear();
                return;
            }

            dict.Clear();

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance);
            MessageBroker.Instance.Publish(instance, message);
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
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ArrayFieldChangeTranspiler&lt;EquipmentElement, ItemSlotsArrayUpdated&gt;(instructions, nameof(Equipment._itemSlots));
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ArrayFieldChangeTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var loadStack = new Stack<CodeInstruction>();
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var arrayAssignIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ArrayAssignIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));
            GenericPatchHelpers.ArrayChangeInterceptCache.TryAdd(fieldInfo, arrayAssignIntercept);
            // TODO: Implement properly with loading the correct instance onto the stack before call the method

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

                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == fieldInfo)
                {
                    loadStack.Push(instruction);
                }

                yield return instruction;
            }

        }

        /// <summary>
        /// Used to transpile arrays
        /// </summary>
        /// <typeparam name="TItem">Type of the collection items</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change has to extend <see cref="GenericArrayChangedEvent{TInstance, TValue}"/></typeparam>
        /// <param name="instructions">CodeInstructions provided by the calling HarmonyTranspiler</param>
        /// <remarks> Calls should look like:<br /><br />
        /// [HarmonyTranspiler] <br />
        /// static IEnumerable&lt;CodeInstruction&gt; WorkshopTranspiler(IEnumerable&lt;CodeInstruction&gt; instructions) <br />
        /// => ArrayPropertyChangeTranspiler&lt;EquipmentElement, ItemSlotsArrayUpdated&gt;(instructions, nameof(Equipment._itemSlots));
        /// </remarks>
        /// <returns>The CodeInstructions</returns>
        public static IEnumerable<CodeInstruction> ArrayPropertyChangeTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string propertyName)
            where TMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var loadStack = new Stack<CodeInstruction>();
            var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
            var arrayAssignIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(ArrayAssignIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));
            GenericPatchHelpers.ArrayChangeInterceptCache.TryAdd(propertyInfo, arrayAssignIntercept);
            // TODO: Implement properly with loading the correct instance onto the stack before call the method

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

                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as PropertyInfo == propertyInfo)
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
            var patchedInstructions = ArrayPropertyChangeTranspiler<TItem, TChangeMessage>(instructions, propertyName);
            return PropertyTranspiler<TItem[], TSetMessage>(patchedInstructions, propertyName);
        }

        public static IEnumerable<CodeInstruction> ArrayFieldSetTranspiler<TItem, TSetMessage, TChangeMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TSetMessage : GenericEvent<TInstance, TItem[]>
            where TChangeMessage : GenericArrayChangedEvent<TInstance, TItem>
        {
            var patchedInstructions = ArrayFieldChangeTranspiler<TItem, TChangeMessage>(instructions, fieldName);
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
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
                return;
            }
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, item, index);
            MessageBroker.Instance.Publish(instance, message);

            items[index] = item;
        }
        /// <summary>
        /// Intercept that gets called when SetPropertyValue is called on a PropertyOwner field
        /// </summary>
        /// <typeparam name="TItem">Type of the property key</typeparam>
        /// <typeparam name="TMessage">Message to be published on Change</typeparam>
        /// <param name="owner">The PropertyOwner being modified</param>
        /// <param name="attribute">The key being set</param>
        /// <param name="value">The value being set</param>
        /// <param name="instance">The class that holds the PropertyOwner field</param>
        /// <remarks>
        /// Can be called from other methods if the generic transpiler is not enough <br/>
        /// Example: <br/>
        /// public static void PropertyOwnerSetIntercept(PropertyOwner&lt;TraitObject&gt; owner, TraitObject attribute, int value, CharacterObject instance) <br/>
        /// => PropertyOwnerSetIntercept&lt;TraitObject, CharacterObject__characterTraits_SetLocalMessage&gt;(owner, attribute, value, instance);
        /// </remarks>
        public static IEnumerable<CodeInstruction> PropertyOwnerFieldChangeTranspiler<TItem, TSetMessage>(
    IEnumerable<CodeInstruction> instructions, string fieldName)
    where TItem : MBObjectBase
    where TSetMessage : IEvent
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            var setPropertyMethod = typeof(PropertyOwner<TItem>).GetMethod(nameof(PropertyOwner<TItem>.SetPropertyValue));
            var setIntercept = typeof(GenericPatches<TPatch, TInstance>)
                .GetMethod(nameof(PropertyOwnerSetIntercept))
                .MakeGenericMethod(typeof(TItem), typeof(TSetMessage));

            GenericPatchHelpers.CollectionAddInterceptCache.TryAdd(fieldInfo, setIntercept);

            return PatchInstructions(instructions.ToList(),
                (ci) => IsCorrectField(ci, fieldInfo),
                setPropertyMethod,
                setIntercept,
                null,
                null);
        }

        public static void PropertyOwnerSetIntercept<TItem, TMessage>(
            PropertyOwner<TItem> owner, TItem attribute, int value, TInstance instance)
            where TItem : MBObjectBase
            where TMessage : IEvent
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                owner.SetPropertyValue(attribute, value);
                return;
            }

            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
                owner.SetPropertyValue(attribute, value);
                return;
            }

            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, attribute, value);
            MessageBroker.Instance.Publish(instance, message);

            owner.SetPropertyValue(attribute, value);
        }
        #endregion

        #region FieldTranspiler
        public static IEnumerable<CodeInstruction> FieldTranspiler<TItem, TMessage>(IEnumerable<CodeInstruction> instructions, string fieldName)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
            AddToFieldCache(fieldInfo);
            var fieldIntercept = typeof(GenericPatches<TPatch, TInstance>).GetMethod(nameof(FieldIntercept)).MakeGenericMethod(typeof(TItem), typeof(TMessage));

            // Used for easier testing
            if (!GenericPatchHelpers.FieldInterceptCache.ContainsKey(fieldInfo))
                GenericPatchHelpers.FieldInterceptCache.TryAdd(fieldInfo, fieldIntercept);
            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(fieldInfo))
                {
                    var loadInst = new CodeInstruction(OpCodes.Ldstr, fieldName);

                    if (instruction.labels.Any())
                        loadInst.labels = instruction.labels.ToList();
                    yield return loadInst;
                    var interceptInst = new CodeInstruction(OpCodes.Call, fieldIntercept);
                    yield return interceptInst;

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
            if ((fieldValue == null && item == null) || item?.Equals(fieldValue) == true)
                return;

            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                fieldInfo.SetValue(instance, item);
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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
            if ((propertyValue == null && item == null) || item?.Equals(propertyValue) == true)
                return;

            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                propertyInfo.SetValue(instance, item);
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
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

        #region PropertyPrefix
        public static void PropertyPrefix<TItem, TMessage>(TInstance instance, TItem value)
            where TMessage : GenericEvent<TInstance, TItem>
        {
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return;
            }
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated unmanaged {type}", typeof(TItem));
                return;
            }

            // TODO: Add some way to verify value has changed to prevent unecessary message flood
            var message = (TMessage)Activator.CreateInstance(typeof(TMessage), instance, value);
            MessageBroker.Instance.Publish(instance, message);
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

    public class GenericPatchHelpers
    {
        public static ConcurrentDictionary<FieldInfo, MethodInfo> FieldInterceptCache = new ConcurrentDictionary<FieldInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> CollectionAddInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> CollectionRemoveInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> ArrayChangeInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();

        // Dictionary intercepts take (instance, dict, ...) rather than the (collection, item, instance)
        // order of the list/queue caches above, so they get their own caches to avoid signature confusion
        public static ConcurrentDictionary<MemberInfo, MethodInfo> DictionaryAddInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> DictionarySetItemInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> DictionaryRemoveInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
        public static ConcurrentDictionary<MemberInfo, MethodInfo> DictionaryClearInterceptCache = new ConcurrentDictionary<MemberInfo, MethodInfo>();
    }
}
