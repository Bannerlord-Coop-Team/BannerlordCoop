using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
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
    public abstract class CoopManaged<T> where T : class
    {
        /// <summary>
        ///     Creates a new rule set for method calls when the caller is equal to <paramref name="eTriggerOrigin"/>.
        /// </summary>
        /// <param name="eTriggerOrigin"></param>
        /// <returns></returns>
        public static ActionTriggerOrigin When(ETriggerOrigin eTriggerOrigin)
        {
            return _callers[eTriggerOrigin];
        }

        public CoopManaged([NotNull] T instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));

            foreach (MethodId methodId in PatchedMethods())
            {
                MethodAccess method = MethodRegistry.IdToMethod[methodId];
                method.SetHandler(_instance, (args) => RuntimeDispatch((object[])args));
            }
        }

        private void RuntimeDispatch(object[] args)
        {
            
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

        public static void InitPatches(Type derivedType)
        {
        }

        private static Dictionary<ETriggerOrigin, ActionTriggerOrigin> _callers = new Dictionary<ETriggerOrigin, ActionTriggerOrigin>()
        {
            {ETriggerOrigin.Local, new ActionTriggerOrigin()},
            {ETriggerOrigin.Authoritative, new ActionTriggerOrigin()}
        };
        
        private readonly T _instance;
    }

    
    
    
    
    
    
    
    
    
}