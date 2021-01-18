using System;
using System.Collections.Generic;
using System.Linq;
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
        public static MethodAccess Setter(string sPropertyName)
        {
            return new PropertyPatch(typeof(T)).InterceptSetter(sPropertyName).Setters.First();
        }
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
                EvaluatedActionBehaviour behaviour = Evaluate(_callers[ETriggerOrigin.Local].GetBehaviour(methodId)); // TODO: caller evaluation
                method.SetHandler(_instance, (args) => RuntimeDispatch(behaviour, (object[])args));
            }
        }

        private ECallPropagation RuntimeDispatch(EvaluatedActionBehaviour behaviour, object[] args)
        {
            return behaviour.CallBehaviour;
        }

        private EvaluatedActionBehaviour Evaluate(IEnumerable<ActionBehaviour> behaviours)
        {
            bool doCallOriginal = true;
            foreach (ActionBehaviour behaviour in behaviours)
            {
                doCallOriginal = behaviour.CallPropagationBehaviour == ECallPropagation.CallOriginal;
            }
            
            return new EvaluatedActionBehaviour()
            {
                CallBehaviour = doCallOriginal ? ECallPropagation.CallOriginal : ECallPropagation.Suppress
            };
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

        private static readonly Dictionary<ETriggerOrigin, ActionTriggerOrigin> _callers = new Dictionary<ETriggerOrigin, ActionTriggerOrigin>()
        {
            {ETriggerOrigin.Local, new ActionTriggerOrigin()},
            {ETriggerOrigin.Authoritative, new ActionTriggerOrigin()}
        };
        
        private readonly T _instance;
    }

    struct EvaluatedActionBehaviour
    {
        public ECallPropagation CallBehaviour;
    }
    
    
    
    
    
    
    
    
}