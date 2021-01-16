using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync;

namespace CoopFramework
{
    /// <summary>
    ///     Base class to extend a type with synchronization functionality through the Coop server. Configuration
    ///     is done statically following a builder pattern:
    ///
    /// <code>
    /// </code>
    ///     
    /// </summary>
    public class Syncable<T> where T : class
    {
        /// <summary>
        ///     Creates a new rule set for method calls when the caller is equal to <paramref name="eCaller"/>.
        /// </summary>
        /// <param name="eCaller"></param>
        /// <returns></returns>
        public static SyncableMethodCaller When(ECaller eCaller)
        {
            return _callers[eCaller];
        }

        public Syncable([NotNull] T instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        [PatchInitializer]
        private static void Init()
        {
            
        }

        private static Dictionary<ECaller, SyncableMethodCaller> _callers = new Dictionary<ECaller, SyncableMethodCaller>()
        {
            {ECaller.Local, new SyncableMethodCaller()},
            {ECaller.Authoritative, new SyncableMethodCaller()}
        };
        
        private readonly T _instance;
    }

    public class SyncableMethodCaller
    {
        public SyncableMethodBehaviour Calls(IEnumerable<MethodAccess> methods)
        {
            var behaviour = new SyncableMethodBehaviour();
            foreach (var method in methods)
            {
                Register(method.Id, behaviour);
            }
            return behaviour;
        }
        
        private void Register(MethodId key, SyncableMethodBehaviour behaviour)
        {
            if (!_behaviours.TryGetValue(key, out var methodBehaviours))
            {
                methodBehaviours = new List<SyncableMethodBehaviour>();
                _behaviours.Add(key, methodBehaviours);
            }
            methodBehaviours.Add(behaviour);
        }
        
        private Dictionary<MethodId, List<SyncableMethodBehaviour>> _behaviours = new Dictionary<MethodId, List<SyncableMethodBehaviour>>();
    }
    
    public class SyncableMethodBehaviour
    {
        public SyncableMethodBehaviour Execute()
        {
            ExecuteMethod = true;
            return this;
        }

        public SyncableMethodBehaviour DelegateTo(Action<object, IPendingMethodCall> handler)
        {
            MethodCallHandler = handler;
            return this;
        }

        public bool ExecuteMethod { get; set; } = true;
        public Action<object, IPendingMethodCall> MethodCallHandler { get; set; } = null;
    }
    
    public enum ECaller
    {
        Local,
        Authoritative
    }
    
    public interface IPendingMethodCall
    {
        void Execute();
        void Broadcast();
    }
    
    
}