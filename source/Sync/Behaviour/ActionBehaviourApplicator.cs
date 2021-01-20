using System;
using System.Collections.Generic;
using NLog;

namespace Sync.Behaviour
{
    public static class ActionBehaviourApplicator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public static void Apply(Type behaviourCreator, MethodAccess method, List<CallBehaviour> behaviours)
        {
            Logger.Debug("{origin} is patching {method}...", behaviourCreator, method);
            foreach (CallBehaviour behaviour in behaviours)
            {
                Logger.Debug("  ", behaviourCreator, method);
            }
        }
    }
}