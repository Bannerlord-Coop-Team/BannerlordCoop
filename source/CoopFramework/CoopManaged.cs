using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using Sync;
using Sync.Behaviour;

namespace CoopFramework
{
    /// <summary>
    ///     Base class to extend a type to be managed by the Coop framework.
    ///     <code>
    /// </code>
    /// </summary>
    public abstract class CoopManaged<TSelf, TExtended> where TExtended : class
    {
        /// <summary>
        ///     Creates synchronization for a given instance of <typeparamref name="TExtended" />.
        /// </summary>
        /// <param name="instance">Instance that should be synchronized.</param>
        /// <exception cref="ArgumentOutOfRangeException">When the instance is null.</exception>
        protected CoopManaged([NotNull] TExtended instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Instance = new WeakReference<TExtended>(instance, true);
            m_GetSync = new Lazy<Func<ISynchronization>>(FindSyncFactory);

            SetupHandlers(this);
        }

        #region Patcher

        /// <summary>
        ///     Enables an automatic injection of this synchronization class into every instance of <see cref="TExtended" />
        ///     that is being created.
        /// </summary>
        /// <param name="factoryMethod">Factory method that creates an instance of the concrete inheriting class."/></param>
        protected static void AutoWrapAllInstances(Func<TExtended, CoopManaged<TSelf, TExtended>> factoryMethod)
        {
            if (m_ConstructorPatch != null || m_DestructorPatch != null)
                throw new Exception($"Constructors for {nameof(TExtended)} are already patched!");

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
        protected static MethodAccess Setter(string sPropertyName)
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
        protected static MethodAccess Method(string sMethodName)
        {
            return new MethodPatch<TSelf>(typeof(TExtended)).Intercept(sMethodName).Postfix(sMethodName).Methods
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
        protected static FieldAccess Field<TField>(string sFieldName)
        {
            var info = AccessTools.Field(typeof(TExtended), sFieldName);
            if (info == null) throw new Exception($"Field {typeof(TExtended)}.{sFieldName} not found.");
            if (info.FieldType != typeof(TField))
                throw new Exception(
                    $"Unexpected field type for {typeof(TExtended)}.{sFieldName}. Expected {typeof(TField)}, got {info.FieldType}.");
            return new FieldAccess<TExtended, TField>(info);
        }

        /// <summary>
        ///     Starting point of a patch for any action. The patch will only be active if the context of the call
        ///     is equal to <paramref name="eOriginator" />.
        /// </summary>
        /// <param name="eOriginator"></param>
        /// <returns></returns>
        protected static ActionBehaviourBuilder When(EOriginator eOriginator)
        {
            var builder = new ActionBehaviourBuilder((eOrigin, _) => eOrigin == eOriginator);
            m_DefinedBehaviours.Add(builder);
            return builder;
        }

        #endregion

        #region Object instances

        /// <summary>
        ///     Returns the instance that is being managed by this <see cref="TSelf" />.
        /// </summary>
        [NotNull]
        protected WeakReference<TExtended> Instance { get; set; }

        /// <summary>
        ///     Returns all instances of this <see cref="TSelf" /> that very automatically created because
        ///     <see cref="AutoWrapAllInstances" /> is enabled.
        /// </summary>
        public static IReadOnlyCollection<CoopManaged<TSelf, TExtended>> AutoWrappedInstances => m_AutoWrappedInstances;

        public static readonly FieldChangeStack FieldStack = new FieldChangeStack();

        #endregion

        #region Private

        private static void OnConstructed(CoopManaged<TSelf, TExtended> newInstance)
        {
            m_AutoWrappedInstances.Add(newInstance);
        }

        private static ECallPropagation Dispatch(CoopManaged<TSelf, TExtended> self,
            EOriginator eOriginator,
            MethodAccess methodAccess,
            List<CallBehaviourBuilder> behaviours,
            object[] args)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.Instance.TryGetTarget(out instanceResolved))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                return ECallPropagation.Suppress; // Will not work anyways
            }

            var sync = self != null ? self.m_GetSync.Value?.Invoke() : m_GetSyncStatic.Value?.Invoke();
            foreach (var behaviour in behaviours)
            {
                if (!behaviour.DoesBehaviourApply(eOriginator, instanceResolved)) continue;
                if (behaviour.DoBroadcast)
                {
                    if (sync == null)
                        throw new SynchronizationNotInitializedException(
                            "No ISynchronization implementation was provided. Unable to use the synchronization behaviours.");
                    sync.Broadcast(methodAccess.Id, instanceResolved, args);
                }

                if (behaviour.MethodCallHandlerInstance != null)
                    return behaviour.MethodCallHandlerInstance.Invoke(self,
                        new PendingMethodCall(methodAccess.Id, sync, instanceResolved, args));
                if (behaviour.MethodCallHandler != null)
                    return behaviour.MethodCallHandler.Invoke(new PendingMethodCall(methodAccess.Id, sync,
                        instanceResolved,
                        args));
                if (behaviour.CallPropagationBehaviour == ECallPropagation.Suppress) return ECallPropagation.Suppress;
            }

            return ECallPropagation.CallOriginal;
        }

        private void OnBeforeFinalize([NotNull] TExtended instance)
        {
            foreach (var behaviour in m_DefinedBehaviours)
            {
                foreach (var accessor in behaviour.FieldChangeAction.SelectMany(f => f.Value.Accessors))
                    RemoveHandlers(instance, accessor);

                foreach (var accessor in behaviour.CallBehaviours.Select(pair => Registry.IdToMethod[pair.Key]))
                    RemoveHandlers(instance, accessor);
            }

            Instance = new WeakReference<TExtended>(null);
        }

        private static void RemoveHandlers(TExtended instance, MethodAccess access)
        {
            if (instance == null)
            {
                access.Prefix.RemoveGlobalHandler();
                access.Postfix.RemoveGlobalHandler();
            }
            else
            {
                access.Prefix.RemoveHandler(instance);
                access.Postfix.RemoveHandler(instance);
            }
        }

        private static readonly List<ActionBehaviourBuilder> m_DefinedBehaviours = new List<ActionBehaviourBuilder>();

        private static readonly List<CoopManaged<TSelf, TExtended>> m_AutoWrappedInstances =
            new List<CoopManaged<TSelf, TExtended>>();

        private static ConstructorPatch<TSelf> m_ConstructorPatch;
        private static DestructorPatch<TSelf> m_DestructorPatch;

        private class PendingMethodCall : IPendingMethodCall
        {
            [CanBeNull] private readonly ISynchronization m_Sync;

            public PendingMethodCall(MethodId method, [CanBeNull] ISynchronization sync, [CanBeNull] object instance,
                [NotNull] object[] args)
            {
                Id = method;
                m_Sync = sync;
                Instance = instance;
                Parameters = args;
            }

            public void Broadcast()
            {
                if (m_Sync == null)
                    throw new SynchronizationNotInitializedException(
                        "No ISynchronization implementation was provided. Unable to use the synchronization behaviours.");

                m_Sync.Broadcast(Id, Instance, Parameters);
            }

            public object Instance { get; }
            public object[] Parameters { get; }
            public MethodId Id { get; }
        }

        private static void HookIntoObjectLifetime(Func<TExtended, CoopManaged<TSelf, TExtended>> factoryMethod)
        {
            m_ConstructorPatch = new ConstructorPatch<TSelf>(typeof(TExtended)).PostfixAll();
            if (!m_ConstructorPatch.Methods.Any())
                throw new Exception(
                    $"Class {typeof(TExtended)} has no constructor. Cannot wrap instances automatically");

            foreach (var methodAccess in m_ConstructorPatch.Methods)
                methodAccess.Postfix.SetGlobalHandler((origin, instance, args) =>
                {
                    OnConstructed(factoryMethod(instance as TExtended));
                });

            m_DestructorPatch = new DestructorPatch<TSelf>(typeof(TExtended)).Prefix();
            if (!m_DestructorPatch.Methods.Any())
                throw new Exception(
                    $"Class {typeof(TExtended)} has no destructor. Cannot unwrap instances automatically");

            foreach (var methodAccess in m_DestructorPatch.Methods)
                methodAccess.Prefix.SetGlobalHandler((origin, instance, args) =>
                {
                    var managedInstances = m_AutoWrappedInstances
                        .Where(wrapper => wrapper.Instance.TryGetTarget(out var o) && o == instance)
                        .ToList();
                    foreach (var managedInstance in managedInstances)
                    {
                        managedInstance.OnBeforeFinalize(instance as TExtended);
                        m_AutoWrappedInstances.Remove(managedInstance);
                    }

                    return ECallPropagation.CallOriginal; // Always call the original desctructor!
                });
        }

        private static void SetupHandlers([CanBeNull] CoopManaged<TSelf, TExtended> self)
        {
            foreach (var patchedMethod in Util.SortByMethod(m_DefinedBehaviours))
                InitMethodPatches(self, patchedMethod.Key, patchedMethod.Value);


            foreach (var patchedField in Util.SortByField(m_DefinedBehaviours))
                InitFieldPatches(self, patchedField.Key, patchedField.Value);
        }

        private static void InitMethodPatches(
            [CanBeNull] CoopManaged<TSelf, TExtended> self,
            MethodId id,
            [NotNull] List<CallBehaviourBuilder> relevantBehaviours)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.Instance.TryGetTarget(out instanceResolved))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                return;
            }

            var methodAccess = Registry.IdToMethod[id];
            if (self == null)
                // Static patch
                methodAccess.Prefix.SetGlobalHandler((eOrigin, target, args) =>
                {
                    if (target != null)
                        // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                        return ECallPropagation.CallOriginal;

                    return Dispatch(null, eOrigin, methodAccess, relevantBehaviours, args);
                });
            else
                // Instance patch
                methodAccess.Prefix.SetHandler(instanceResolved,
                    (eOrigin, args) => { return Dispatch(self, eOrigin, methodAccess, relevantBehaviours, args); });
        }

        private static void InitFieldPatches(
            [CanBeNull] CoopManaged<TSelf, TExtended> self,
            FieldId id,
            [NotNull] List<FieldActionBehaviourBuilder> relevantBehaviours)
        {
            TExtended instanceResolved = null; // stays null for static calls
            if (self != null && !self.Instance.TryGetTarget(out instanceResolved))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                return;
            }

            var fieldAccess = Registry.IdToField[id];
            var accessors = relevantBehaviours.SelectMany(b => b.Accessors);
            foreach (var accessor in accessors)
            {
                var methodRelevantBehaviours = relevantBehaviours
                    .Where(f => f.Accessors.Contains(accessor))
                    .Select(f => f.Behaviour).ToArray();

                if (self == null)
                {
                    accessor.Prefix.SetGlobalHandler((eOrigin, target, args) =>
                    {
                        if (target != null)
                            // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                            return ECallPropagation.CallOriginal;
                        return DispatchPrefix(null, eOrigin, methodRelevantBehaviours, fieldAccess);
                    });
                    accessor.Postfix.SetGlobalHandler((eOrigin, target, args) =>
                    {
                        if (target != null)
                            // Default behaviour: instance methods are handled by the corresponding CoopManaged instance.
                            return;
                        DispatchPostfix(null, eOrigin, methodRelevantBehaviours);
                    });
                }
                else
                {
                    // Instance patch
                    accessor.Prefix.SetHandler(instanceResolved,
                        (origin, args) =>
                        {
                            return DispatchPrefix(self, origin, methodRelevantBehaviours, fieldAccess);
                        });
                    accessor.Postfix.SetHandler(instanceResolved,
                        (origin, args) => { DispatchPostfix(self, origin, methodRelevantBehaviours); });
                }
            }
        }

        private static void DispatchPostfix(
            [CanBeNull] CoopManaged<TSelf, TExtended> self,
            EOriginator origin,
            FieldBehaviourBuilder[] behaviours)
        {
            foreach (var behaviour in behaviours)
            {
                if (!behaviour.DoesBehaviourApply(origin, self)) return;

                TExtended instanceResolved = null; // stays null for static calls
                if (self != null && !self.Instance.TryGetTarget(out instanceResolved))
                {
                    // The instance went out of scope?
                    Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                    return;
                }

                if (behaviour.DoBroadcast)
                {
                    // TODO:
                }

                FieldStack.PopUntilMarker(behaviour.Action == EFieldChangeAction.Revert);
            }
        }

        private static ECallPropagation DispatchPrefix(
            [CanBeNull] CoopManaged<TSelf, TExtended> self,
            EOriginator origin,
            IEnumerable<FieldBehaviourBuilder> behaviours,
            FieldAccess fieldAccess)
        {
            foreach (var behaviour in behaviours)
            {
                if (!behaviour.DoesBehaviourApply(origin, self)) return ECallPropagation.CallOriginal;

                TExtended instanceResolved = null; // stays null for static calls
                if (self != null && !self.Instance.TryGetTarget(out instanceResolved))
                {
                    // The instance went out of scope?
                    Logger.Warn("Coop synced {Instance} seems to have expired", self.ToString());
                    return ECallPropagation.Suppress; // Will not work anyways
                }

                FieldStack.PushMarker();
                FieldStack.PushValue(fieldAccess, instanceResolved);
            }

            return ECallPropagation.CallOriginal;
        }

        private Func<ISynchronization> FindSyncFactory()
        {
            foreach (var method in typeof(TSelf).GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly |
                                                            BindingFlags.NonPublic))
                if (Attribute.IsDefined(method, typeof(SyncFactoryAttribute)) &&
                    method.ReturnType == typeof(ISynchronization))
                    return () => method.Invoke(this, new object[] { }) as ISynchronization;

            return m_GetSyncStatic?.Value;
        }


        [NotNull] private static readonly Lazy<Func<ISynchronization>> m_GetSyncStatic =
            new Lazy<Func<ISynchronization>>(() =>
            {
                foreach (var method in typeof(TSelf).GetMethods(BindingFlags.Static | BindingFlags.DeclaredOnly |
                                                                BindingFlags.NonPublic))
                    if (Attribute.IsDefined(method, typeof(SyncFactoryAttribute)) &&
                        method.ReturnType == typeof(ISynchronization))
                        return () => method.Invoke(null, new object[] { }) as ISynchronization;

                return null;
            });

        [NotNull] private readonly Lazy<Func<ISynchronization>> m_GetSync;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion
    }
}