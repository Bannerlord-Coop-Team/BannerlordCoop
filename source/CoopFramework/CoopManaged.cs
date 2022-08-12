using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using Sync;
using Sync.Behaviour;
using Sync.Call;
using Sync.Patch;
using Sync.Value;

namespace CoopFramework
{
    /// <summary>
    ///     Base class to extend a type to be managed by the Coop framework. A coop managed class can be extended
    ///     with additional data and generate patches for the original class at runtime. 
    /// </summary>
    public abstract class CoopManaged<TSelf, TExtended> : DefaultConditions 
        where TExtended : class
        where TSelf : class
    {
        #region Instance lookup
        /// <summary>
        ///     All created <see cref="CampaignMapMovement"/> instances.
        /// </summary>
        protected static readonly ConditionalWeakTable<TExtended, TSelf> Instances = new ConditionalWeakTable<TExtended, TSelf>();
        #endregion

        /// <summary>
        ///     Creates synchronization for a given instance of <typeparamref name="TExtended" />.
        /// </summary>
        /// <param name="instance">Instance that should be synchronized.</param>
        /// <exception cref="ArgumentOutOfRangeException">When the instance is null.</exception>
        protected CoopManaged(TExtended instance)
        {
            if (instance == null) 
            {
                throw new ArgumentNullException(nameof(instance));
            }
            if (Instances.TryGetValue(instance, out TSelf existingInstance))
            {
                throw new InvalidOperationException($"{instance} already has a corresponding CoopManaged instance {existingInstance}. Cannot create another one.");
            }

            Instances.Add(instance, this as TSelf);
            ManagedInstance = new WeakReference<TExtended>(instance, true);
            SetupHandlers(this);
        }

        #region Patcher
        /// <summary>
        ///     Enables an automatic injection of this synchronization class into every instance of <see cref="TExtended" />
        ///     that is being created.
        /// </summary>
        /// <param name="factoryMethod">Factory method that creates an instance of the concrete inheriting class."/></param>
        protected static void AutoWrapAllInstances(Func<TExtended, TSelf> factoryMethod)
        {
            HookIntoObjectLifetime(factoryMethod);
        }

        /// <summary>
        ///     Applies all static patches defined before this call.
        ///     ATTENTION: This function is not called automatically! i.e. static patches will not work unless this
        ///     method is called after they where defined.
        /// </summary>
        protected static void ApplyStaticPatches()
        {
            SetupHandlers(null);
        }

        /// <summary>
        ///     Patches a setter of a property on the extended class. Please of the nameof operator instead of raw
        ///     strings if possible. This allows for compile time errors with updated game versions:
        ///     <code>Setter(nameof(T.Foo));</code>
        /// </summary>
        /// <param name="sPropertyName"></param>
        /// <returns></returns>
        protected static PatchedInvokable Setter(string sPropertyName)
        {
            return new PropertyPatch<TSelf>(typeof(TExtended)).InterceptSetter(sPropertyName)
                .PostfixSetter(sPropertyName).Setters.First();
        }

        /// <summary>
        ///     Patches a method on the extended class with a prefix. Please of the nameof operator instead of raw
        ///     strings if possible. This allows for compile time errors with updated game versions:
        ///     <code>Method(nameof(T.Foo));</code>
        /// </summary>
        /// <param name="sMethodName"></param>
        /// <returns></returns>
        protected static PatchedInvokable Method(string sMethodName)
        {
            return new MethodPatch<TSelf>(typeof(TExtended)).Intercept(sMethodName).Postfix(sMethodName).Methods
                .First();
        }
        /// <summary>
        ///     Patches a method of an arbitrary class with a prefix.
        /// </summary>
        /// <param name="sMethodName"></param>
        /// <typeparam name="TDeclaringClass"></typeparam>
        /// <returns></returns>
        protected static PatchedInvokable Method<TDeclaringClass>(string sMethodName) where TDeclaringClass : class
        {
            return new MethodPatch<TSelf>(typeof(TDeclaringClass)).Intercept(sMethodName).Postfix(sMethodName).Methods
                .First();
        }
        /// <summary>
        ///     Patches a method of an arbitrary class with a prefix.
        /// </summary>
        /// <param name="sMethodName"></param>
        /// <typeparam name="TDeclaringClass"></typeparam>
        /// <returns></returns>
        protected static PatchedInvokable Method(Type declaringClass, string sMethodName)
        {
            return new MethodPatch<TSelf>(declaringClass).Intercept(sMethodName).Postfix(sMethodName).Methods
                .First();
        }

        /// <summary>
        ///     Sets up a field to be monitored for changes. Please of the nameof operator instead of raw
        ///     strings if possible. This allows for compile time errors with updated game versions:
        ///     <code>Field(nameof(T.Foo));</code>
        /// </summary>
        /// <param name="sFieldName"></param>
        /// <typeparam name="TField"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected static FieldAccess<TExtended, TField> Field<TField>(string sFieldName)
        {
            var info = AccessTools.Field(typeof(TExtended), sFieldName);
            if (info == null) throw new Exception($"Field {typeof(TExtended)}.{sFieldName} not found.");
            if (info.FieldType != typeof(TField))
                throw new Exception(
                    $"Unexpected field type for {typeof(TExtended)}.{sFieldName}. Expected {typeof(TField)}, got {info.FieldType}.");
            return new FieldAccess<TExtended, TField>(info);
        }

        /// <summary>
        ///     Declares a Coop patch that is active when the
        ///     <param name="condition"></param>
        ///     evaluates
        ///     to true. See <see cref="DefaultConditions" /> for predefined conditions.
        ///     The condition is evaluated when the patched action is executed.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        protected static ActionBehaviourBuilder When(Condition condition)
        {
            var builder = new ActionBehaviourBuilder(condition);
            m_DefinedBehaviours.Add(builder);
            return builder;
        }
            

        #endregion

        #region Object instances

        /// <summary>
        ///     Get the instance that is being managed by this wrapper. Since <see cref="CoopManaged{TSelf,TExtended}" />
        ///     only keeps a <see cref="WeakReference{TExtended}" />, the managed instance might not exist anymore.
        ///     A return value of false indicates an issue with the lifetime management.
        /// </summary>
        /// <param name="resolvedInstance">The managed instance or null.</param>
        /// <returns></returns>
        protected bool TryGetInstance(out TExtended resolvedInstance)
        {
            if (ManagedInstance.TryGetTarget(out resolvedInstance)) return true;

            Logger.Debug("Coop synced {Instance} seems to have expired. Removed.", ToString());
            lock (m_AutoWrappedInstances)
            {
                // If the wrapper was automatically created, delete it
                m_AutoWrappedInstances.Remove(this);
            }

            return false;
        }

        /// <summary>
        ///     If the <typeparamref name="TExtended" /> does not implement a destructor, the lifetime of instances
        ///     needs to be tracked manually. This constant defines the interval in which a check is performed to
        ///     release automatically created <typeparamref name="TSelf" /> wrapper instance.
        /// </summary>
        public const int GCInterval_ms = 5000;

        /// <summary>
        ///     Returns all instances of this <see cref="TSelf" /> that very automatically created because
        ///     <see cref="AutoWrapAllInstances" /> is enabled.
        ///     ATTENTION: This collection might be modified from another thread! Lock before use.
        /// </summary>
        public static IReadOnlyCollection<CoopManaged<TSelf, TExtended>> AutoWrappedInstances => m_AutoWrappedInstances;

        #endregion

        #region Private

        /// <summary>
        ///     Ensures that the static constructors of the inheriting class are called in order to initialize
        ///     the patch definitions. Needs to be called exactly once on startup. Needs to be protected since
        ///     private static methods cannot be found using reflection.
        /// </summary>
        [PatchInitializer]
        protected static void RunStaticConstructor()
        {
            RuntimeHelpers.RunClassConstructor(typeof(TSelf).TypeHandle);

            // Register relation between fields and their accessors.
            foreach (var patchedField in Util.SortByField(m_DefinedBehaviours))
            foreach (var behaviour in patchedField.Value)
            foreach (var accessor in behaviour.Accessors)
                Registry.AddRelation(accessor.Id, patchedField.Key);
        }

        /// <summary>
        ///     Returns the instance that is being managed by this <see cref="TSelf" />.
        /// </summary>
        private WeakReference<TExtended> ManagedInstance { get; set; }

        /// <summary>
        ///     Called when a new instance of <see cref="TSelf" /> was automatically created.
        /// </summary>
        /// <param name="newInstance"></param>
        private static void OnAutoConstructed(TSelf newInstance)
        {
            lock (m_AutoWrappedInstances)
            {
                m_AutoWrappedInstances.Add(newInstance as CoopManaged<TSelf, TExtended>);
            }
        }

        /// <summary>
        ///     Called to dispatch a pending method call (prefix). The statically configured behaviours are evaluated
        ///     and method call treated according to the result.
        /// </summary>
        /// <param name="self">The instance of the CoopManager that wraps the instance of the method call. null for static calls.</param>
        /// <param name="eOriginator">The originator of the action.</param>
        /// <param name="invokableId">The id of the method that is being called.</param>
        /// <param name="behaviours">The statically configured behaviours that apply to this call.</param>
        /// <param name="args">Arguments to the method call.</param>
        /// <returns></returns>
        private static ECallPropagation Dispatch(
            CoopManaged<TSelf, TExtended> self,
            EOriginator eOriginator,
            InvokableId invokableId,
            List<CallBehaviourBuilder> behaviours,
            object[] args)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.TryGetInstance(out instanceResolved))
                return ECallPropagation.Skip; // Will not work anyways

            // Evaluate behaviours
            foreach (var behaviour in behaviours)
            {
                if (!behaviour.AppliesTo(eOriginator, instanceResolved)) continue;
                if (behaviour.DoBroadcast)
                    behaviour.SynchronizationFactory()?.Broadcast(invokableId, instanceResolved, args);

                if (behaviour.MethodCallHandlerInstance != null)
                    return behaviour.MethodCallHandlerInstance.Invoke(self,
                        new PendingMethodCall(invokableId, behaviour.SynchronizationFactory, instanceResolved, args));
                if (behaviour.MethodCallHandler != null)
                    return behaviour.MethodCallHandler.Invoke(new PendingMethodCall(invokableId,
                        behaviour.SynchronizationFactory,
                        instanceResolved,
                        args));
                if (behaviour.CallPropagationBehaviour == ECallPropagation.Skip) return ECallPropagation.Skip;
            }

            return ECallPropagation.CallOriginal;
        }

        /// <summary>
        ///     Called when an auto wrapped instance is being unregistered.
        /// </summary>
        /// <param name="instance"></param>
        private void OnAutoRemoved(TExtended instance)
        {
            foreach (var behaviour in m_DefinedBehaviours)
            {
                foreach (var accessor in behaviour.FieldChangeAction.SelectMany(f => f.Value.Accessors))
                    RemoveHandlers(instance, accessor);

                foreach (var accessor in behaviour.CallBehaviours.Select(pair =>
                    Registry.IdToInvokable[pair.Key] as PatchedInvokable))
                    RemoveHandlers(instance, accessor);
            }

            ManagedInstance = new WeakReference<TExtended>(null);
        }

        /// <summary>
        ///     Removes all handlers of a given <see cref="PatchedInvokable" />.
        /// </summary>
        /// <param name="instance">Instance whose handlers should be removed. Null for static handlers.</param>
        /// <param name="invokable"></param>
        private static void RemoveHandlers(TExtended instance, PatchedInvokable invokable)
        {
            if (instance == null)
            {
                invokable.Prefix.RemoveGlobalHandler();
                invokable.Postfix.RemoveGlobalHandler();
            }
            else
            {
                invokable.Prefix.RemoveHandler(instance);
                invokable.Postfix.RemoveHandler(instance);
            }
        }

        private static readonly List<ActionBehaviourBuilder> m_DefinedBehaviours = new List<ActionBehaviourBuilder>();

        private static readonly List<CoopManaged<TSelf, TExtended>> m_AutoWrappedInstances =
            new List<CoopManaged<TSelf, TExtended>>();

        /// <summary>
        ///     Internal implementation for the method call interface.
        /// </summary>
        private class PendingMethodCall : IPendingMethodCall
        {
            private readonly Func<ISynchronization> m_SyncFactory;

            public PendingMethodCall(InvokableId invokable, Func<ISynchronization> syncFactory,
                object instance,
                object[] args)
            {
                Id = invokable;
                m_SyncFactory = syncFactory;
                Instance = instance;
                Parameters = args;
            }

            public void Broadcast()
            {
                if (m_SyncFactory?.Invoke() == null)
                    throw new SynchronizationNotInitializedException(
                        "No ISynchronization implementation was provided. Unable to use the synchronization behaviours.");

                m_SyncFactory().Broadcast(Id, Instance, Parameters);
            }

            public object Instance { get; }
            public object[] Parameters { get; }
            public InvokableId Id { get; }
        }

        /// <summary>
        ///     Installs observers or necessary patches to create an instance of <typeparamref name="TSelf" /> whenever
        ///     an instance of <typeparamref name="TExtended" /> needs wrapping.
        /// </summary>
        /// <param name="factoryMethod">Factory to create a <typeparamref name="TSelf" /> instance.</param>
        private static void HookIntoObjectLifetime(Func<TExtended, TSelf> factoryMethod)
        {
            m_LifetimeObserver = new ObjectLifetimeObserver<TExtended>();
            m_LifetimeObserver.AfterCreateObject += instance => OnAutoConstructed(factoryMethod(instance));
            m_LifetimeObserver.AfterRemoveObject += instance =>
            {
                lock (m_AutoWrappedInstances)
                {
                    var managedInstances = m_AutoWrappedInstances
                        .Where(wrapper => wrapper.ManagedInstance.TryGetTarget(out var o) && o == instance)
                        .ToList();
                    foreach (var managedInstance in managedInstances)
                    {
                        managedInstance.OnAutoRemoved(instance);
                        m_AutoWrappedInstances.Remove(managedInstance);
                    }
                }
            };

            m_LifetimeObserver.PatchConstruction();
            if (!m_LifetimeObserver.PatchDeconstruction())
            {
                Logger.Debug(
                    $"Class {typeof(TExtended)} has no destructor. Auto unwrapping not possible. Registering with GC to prevent memory leaks.");
                
                
                GCTask = Task.Run(async () =>
                {
                    while (!GCTaskCancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(GCInterval_ms);
                        UnwrapUnusedAutoInstances();
                    }
                });
            }
        }

        /// <summary>
        ///     Cleans up any automatically created <typeparamref name="TSelf" /> instances that are no longer bound to
        ///     a managed instance.
        /// </summary>
        private static void UnwrapUnusedAutoInstances()
        {
            lock (m_AutoWrappedInstances)
            {
                m_AutoWrappedInstances.RemoveAll(managed =>
                {
                    return !managed.ManagedInstance.TryGetTarget(out var intance);
                }
                );
            }
        }

        /// <summary>
        ///     Setup handlers for the statically configured patches.
        /// </summary>
        /// <param name="self">Instance to setup the handlers for or null for static handlers.</param>
        private static void SetupHandlers(CoopManaged<TSelf, TExtended> self)
        {
            foreach (var patchedMethod in Util.SortByMethod(m_DefinedBehaviours))
            {
                var invokable = Registry.IdToInvokable[patchedMethod.Key];
                if (invokable is PatchedInvokable patch)
                    SetupMethodHandlers(self, patch, patchedMethod.Value);
                else
                    throw new Exception($"{invokable} is of an unexpected type.");
            }


            foreach (var patchedField in Util.SortByField(m_DefinedBehaviours))
                SetupFieldHandlers(self, patchedField.Key, patchedField.Value);
        }

        /// <summary>
        ///     Setup handlers for the given method patches.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="patchedInvokable"></param>
        /// <param name="relevantBehaviours"></param>
        private static void SetupMethodHandlers(
            CoopManaged<TSelf, TExtended> self,
            PatchedInvokable patchedInvokable,
            List<CallBehaviourBuilder> relevantBehaviours)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.TryGetInstance(out instanceResolved)) return;
            if (self == null)
                // Static patch
                patchedInvokable.Prefix.SetGlobalHandler((eOrigin, target, args) =>
                {
                    if (!CoopFramework.IsEnabled || target != null)
                        // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                        return ECallPropagation.CallOriginal;

                    return Dispatch(null, eOrigin, patchedInvokable.Id, relevantBehaviours, args);
                });
            else
                // Instance patch
                patchedInvokable.Prefix.SetHandler(instanceResolved,
                    (eOrigin, args) =>
                    {
                        if (!CoopFramework.IsEnabled) return ECallPropagation.CallOriginal;
                        return Dispatch(self, eOrigin, patchedInvokable.Id, relevantBehaviours, args);
                    });
        }

        /// <summary>
        ///     Setup handlers for the given field patches.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="id"></param>
        /// <param name="relevantBehaviours"></param>
        private static void SetupFieldHandlers(
            CoopManaged<TSelf, TExtended> self,
            FieldId id,
            List<FieldAccessBehaviourBuilder> relevantBehaviours)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.TryGetInstance(out instanceResolved)) return;

            var field = Registry.IdToField[id];
            var accessors = relevantBehaviours.SelectMany(b => b.Accessors);
            foreach (var accessor in accessors)
            {
                var methodRelevantBehaviours = relevantBehaviours
                    .Where(f => f.Accessors.Contains(accessor))
                    .Select(f => f.Behaviour).ToArray();

                if (self == null)
                {
                    var prefix = accessor.Prefix.GlobalPrefixHandler;
                    if (prefix != null)
                        // Allow for incremental patching by combining the existing handler with the new one
                        accessor.Prefix.RemoveGlobalHandler();
                    accessor.Prefix.SetGlobalHandler((eOrigin, target, args) =>
                    {
                        if (!CoopFramework.IsEnabled || target != null)
                            // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                            return ECallPropagation.CallOriginal;
                        if (prefix?.Invoke(eOrigin, null, args) == ECallPropagation.Skip) return ECallPropagation.Skip;
                        return DispatchPrefix(null, eOrigin, methodRelevantBehaviours, field);
                    });

                    var postfix = accessor.Postfix.GlobalHandler;
                    if (postfix != null)
                        // Allow for incremental patching by combining the existing handler with the new one
                        accessor.Postfix.RemoveGlobalHandler();
                    accessor.Postfix.SetGlobalHandler((eOrigin, target, args) =>
                    {
                        if (!CoopFramework.IsEnabled || target != null)
                            // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                            return;
                        DispatchPostfix(null, eOrigin, methodRelevantBehaviours);
                        postfix?.Invoke(eOrigin, null, args);
                    });
                }
                else
                {
                    // Instance patch
                    var prefix = accessor.Prefix.GetHandler(instanceResolved);
                    if (prefix != null)
                        // Allow for incremental patching by combining the existing handler with the new one
                        accessor.Prefix.RemoveHandler(instanceResolved);

                    accessor.Prefix.SetHandler(instanceResolved,
                        (origin, args) =>
                        {
                            if (!CoopFramework.IsEnabled) return ECallPropagation.CallOriginal;
                            if (prefix?.Invoke(origin, args) == ECallPropagation.Skip) return ECallPropagation.Skip;
                            return DispatchPrefix(self, origin, methodRelevantBehaviours, field);
                        });

                    var postfix = accessor.Postfix.GetHandler(instanceResolved);
                    if (postfix != null)
                        // Allow for incremental patching by combining the existing handler with the new one
                        accessor.Postfix.RemoveHandler(instanceResolved);

                    accessor.Postfix.SetHandler(instanceResolved,
                        (origin, args) =>
                        {
                            if (!CoopFramework.IsEnabled) return;

                            DispatchPostfix(self, origin, methodRelevantBehaviours);
                            postfix?.Invoke(origin, args);
                        });
                }
            }
        }


        /// <summary>
        ///     Called to dispatch the postfix of a method call that potentially changed a field value.
        /// </summary>
        /// <param name="self">The instance of the CoopManager that wraps the instance of the method call. null for static calls.</param>
        /// <param name="eOriginator">The originator of the action.</param>
        /// <param name="behaviours">Behaviours that apply to this field.</param>
        private static void DispatchPostfix(
            CoopManaged<TSelf, TExtended> self,
            EOriginator eOriginator,
            FieldBehaviourBuilder[] behaviours)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.ManagedInstance.TryGetTarget(out instanceResolved))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                return; // Will not work anyways
            }

            foreach (var behaviour in behaviours)
            {
                if (!behaviour.AppliesTo(eOriginator, instanceResolved)) return;

                var changes = FieldStack.PopUntilMarker(behaviour.Action == EFieldChangeAction.Revert);
                if (behaviour.DoBroadcast) behaviour.SynchronizationFactory()?.Broadcast(changes);
            }
        }

        /// <summary>
        ///     Called to dispatch the prefix of a method call that potentially changes a field value.
        /// </summary>
        /// <param name="self">The instance of the CoopManager that wraps the instance of the method call. null for static calls.</param>
        /// <param name="eOriginator">The originator of the action.</param>
        /// <param name="behaviours">Behaviours that apply to this field.</param>
        /// <param name="field">Access to the value.</param>
        private static ECallPropagation DispatchPrefix(
            CoopManaged<TSelf, TExtended> self,
            EOriginator eOriginator,
            IEnumerable<FieldBehaviourBuilder> behaviours,
            FieldBase field)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.ManagedInstance.TryGetTarget(out instanceResolved))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                return ECallPropagation.Skip; // Will not work anyways
            }

            foreach (var behaviour in behaviours)
            {
                if (!behaviour.AppliesTo(eOriginator, instanceResolved)) return ECallPropagation.CallOriginal;

                FieldStack.PushMarker();
                FieldStack.PushValue(field, instanceResolved);
            }

            return ECallPropagation.CallOriginal;
        }
        public override string ToString()
        {
            return $"CoopManaged: {ManagedInstance}";
        }

        private static ObjectLifetimeObserver<TExtended> m_LifetimeObserver;
        private static Task GCTask;
        private static CancellationToken GCTaskCancellationToken = new CancellationToken();
        private static readonly FieldChangeStack FieldStack = new FieldChangeStack();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion
    }
}