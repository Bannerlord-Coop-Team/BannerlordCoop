using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NLog;
using Sync;
using Sync.Behaviour;

namespace CoopFramework
{
    
    /// <summary>
    ///     Base class to extend a type to be managed by the Coop framework.
    ///
    /// <code>
    /// </code>
    ///     
    /// </summary>
    public abstract class CoopManaged<TSelf, TExtended> where TExtended : class
    {
        #region Patcher
        /// <summary>
        ///     Enables an automatic injection of this synchronization class into every instance of <see cref="TExtended"/>
        ///     that is being created.
        /// </summary>
        /// <param name="factoryMethod">Factory method that creates an instance of the concrete inheriting class."/></param>
        public static void EnabledForAllInstances(Func<TExtended, CoopManaged<TSelf, TExtended>> factoryMethod)
        {
            if (m_ConstructorPatch != null || m_DestructorPatch != null)
            {
                throw new Exception($"Constructors for {nameof(TExtended)} are already patched!");
            }
            
            m_ConstructorPatch = new ConstructorPatch<TSelf>(typeof(TExtended)).PostfixAll();
            if (!m_ConstructorPatch.Methods.Any())
            {
                throw new Exception(
                    $"Class {typeof(TExtended)} has no constructor. Cannot wrap instances automatically");
            }
            foreach (MethodAccess methodAccess in m_ConstructorPatch.Methods)
            {
                methodAccess.Postfix.SetGlobalHandler((origin, instance, args) =>
                {
                    OnConstructed(factoryMethod(instance as TExtended));
                });
            }

            m_DestructorPatch = new DestructorPatch<TSelf>(typeof(TExtended)).Prefix();
            if (!m_DestructorPatch.Methods.Any())
            {
                throw new Exception(
                    $"Class {typeof(TExtended)} has no destructor. Cannot unwrap instances automatically");
            }
            foreach (MethodAccess methodAccess in m_DestructorPatch.Methods)
            {
                methodAccess.Prefix.SetGlobalHandler((origin, instance, args) =>
                {
                    var managedInstances = m_ManagedInstances
                        .Where(wrapper => wrapper.Instance.TryGetTarget(out TExtended o) && o == instance)
                        .ToList();
                    foreach (var managedInstance in managedInstances)
                    {
                        managedInstance.OnBeforeFinalize(instance as TExtended);
                        m_ManagedInstances.Remove(managedInstance);
                    }

                    return ECallPropagation.CallOriginal; // Always call the original desctructor!
                });
            }
        }

        private static void OnConstructed(CoopManaged<TSelf, TExtended> newInstance)
        {
            m_ManagedInstances.Add(newInstance);
        }
        
        /// <summary>
        ///     Patches a setter of a property on the extended class. Please of the nameof operator instead of raw
        ///     strings. This allows for compile time errors with updated game versions:
        /// 
        ///     <code>Setter(nameof(T.Foo));</code>
        /// </summary>
        /// <param name="sPropertyName"></param>
        /// <returns></returns>
        public static MethodAccess Setter(string sPropertyName)
        {
            return new PropertyPatch<TSelf>(typeof(TExtended)).InterceptSetter(sPropertyName).Setters.First();
        }
        /// <summary>
        ///     Starting point of a patch for any action. The patch will only be active if the context of the call
        ///     is equal to <paramref name="eTriggerOrigin"/>.
        /// </summary>
        /// <param name="eTriggerOrigin"></param>
        /// <returns></returns>
        public static ActionTriggerOrigin When(ETriggerOrigin eTriggerOrigin)
        {
            return _callers[eTriggerOrigin];
        }
        #endregion

        /// <summary>
        ///     Creates synchronization for a given instance of <typeparamref name="TExtended"/>.
        /// </summary>
        /// <param name="sync">Implementation of the synchronization. If this is set to null, the synchronization
        /// behaviours may not be used.</param>
        /// <param name="instance">Instance that should be synchronized.</param>
        /// <exception cref="ArgumentOutOfRangeException">When the instance is null.</exception>
        public CoopManaged([CanBeNull] ISynchronization sync, [NotNull] TExtended instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            m_Sync = sync;
            Instance = new WeakReference<TExtended>(instance, true);

            foreach (MethodId methodId in PatchedMethods())
            {
                MethodAccess method = MethodRegistry.IdToMethod[methodId];
                CallBehaviour behaviourLocal = _callers[ETriggerOrigin.Local].GetBehaviour(methodId);
                CallBehaviour behaviourAuth = _callers[ETriggerOrigin.Authoritative].GetBehaviour(methodId);
                method.Prefix.SetHandler(instance, (eOrigin, args) =>
                {
                    switch (eOrigin)
                    {
                        case ETriggerOrigin.Local:
                            return RuntimeDispatch(method, behaviourLocal, args);
                        case ETriggerOrigin.Authoritative:
                            return RuntimeDispatch(method, behaviourAuth, args);
                        default:
                            throw new ArgumentOutOfRangeException(nameof(eOrigin), eOrigin, null);
                    }
                });
            }
        }
        
        /// <summary>
        ///     Returns the synchronized instance.
        /// </summary>
        [NotNull] public WeakReference<TExtended> Instance { get; private set; }

        public static IReadOnlyCollection<CoopManaged<TSelf, TExtended>> ManagedInstances => m_ManagedInstances;

        #region Private
        private ECallPropagation RuntimeDispatch(MethodAccess methodAccess, CallBehaviour behaviour, object[] args)
        {
            if (!Instance.TryGetTarget(out TExtended instance))
            {
                // The instance went out of scope?
                Logger.Warn("Coop synced {Instance} seems to have expired", ToString());
                return ECallPropagation.Suppress; // Will not work anyways
            }
            
            if (behaviour.DoBroadcast)
            {
                if (m_Sync == null)
                {
                    throw new SynchronizationNotInitializedException("No ISynchronization implementation was provided. Unable to use the synchronization behaviours.");
                }
                m_Sync.Broadcast(methodAccess.Id, instance, args);
            }
            if (behaviour.MethodCallHandler != null)
            {
                return behaviour.MethodCallHandler.Invoke(new PendingMethodCall(methodAccess.Id, m_Sync, instance, args));
            }
            return behaviour.CallPropagationBehaviour;
        }

        private IEnumerable<MethodId> PatchedMethods()
        {
            HashSet<MethodId> ids = new HashSet<MethodId>();
            foreach (ActionTriggerOrigin origin in _callers.Values)
            {
                ids.UnionWith(origin.Behaviours.Keys);
            }
            return ids;
        }

        private void OnBeforeFinalize(TExtended instance)
        {
            foreach (MethodId methodId in PatchedMethods())
            {
                MethodAccess method = MethodRegistry.IdToMethod[methodId];
                method.Prefix.RemoveHandler(instance);
            }

            Instance = new WeakReference<TExtended>(null);
        }

        private static readonly Dictionary<ETriggerOrigin, ActionTriggerOrigin> _callers =
            new Dictionary<ETriggerOrigin, ActionTriggerOrigin>()
            {
                {ETriggerOrigin.Local, new ActionTriggerOrigin(true)},
                {ETriggerOrigin.Authoritative, new ActionTriggerOrigin(false)}
            };

        private static readonly List<CoopManaged<TSelf, TExtended>> m_ManagedInstances = new List<CoopManaged<TSelf, TExtended>>();
        private static ConstructorPatch<TSelf> m_ConstructorPatch;
        private static DestructorPatch<TSelf> m_DestructorPatch;
        
        private class PendingMethodCall : IPendingMethodCall
        {
            public PendingMethodCall(MethodId method, [CanBeNull] ISynchronization sync, [CanBeNull] object instance, [NotNull] object[] args)
            {
                m_Method = method;
                m_Sync = sync;
                Instance = instance;
                Parameters = args;
            }
            public void Broadcast()
            {
                if (m_Sync == null)
                {
                    throw new SynchronizationNotInitializedException("No ISynchronization implementation was provided. Unable to use the synchronization behaviours.");
                }
                
                m_Sync.Broadcast(m_Method, Instance, Parameters);
            }

            public object Instance { get; }
            public object[] Parameters { get; }

            [CanBeNull] private readonly ISynchronization m_Sync;

            private readonly MethodId m_Method;
        }

        [CanBeNull] private readonly ISynchronization m_Sync;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion
    }
}