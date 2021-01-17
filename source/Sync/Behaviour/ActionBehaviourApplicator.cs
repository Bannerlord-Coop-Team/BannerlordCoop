using System;
using System.Collections.Generic;
using NLog;

namespace Sync.Behaviour
{
    public static class ActionBehaviourApplicator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public static void Apply(Type behaviourCreator, MethodAccess method, List<ActionBehaviour> behaviours)
        {
            Logger.Debug("{origin} is patching {method}...", behaviourCreator, method);
            foreach (ActionBehaviour behaviour in behaviours)
            {
                Logger.Debug("  ", behaviourCreator, method);
            }
        }
    }
}