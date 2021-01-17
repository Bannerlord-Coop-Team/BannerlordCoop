using System;
using System.Collections.Generic;
using Sync;
using Sync.Behaviour;

namespace CoopFramework
{
    public static class CoopManagedPatcher
    {
        public static void GeneratePatches(Type patchCreator, ETriggerOrigin caller, Dictionary<MethodId, List<ActionBehaviour>> behaviours)
        {
            foreach (KeyValuePair<MethodId,List<ActionBehaviour>> pair in behaviours)
            {
                MethodAccess access = MethodRegistry.IdToMethod[pair.Key];
                ActionBehaviourApplicator.Apply(patchCreator, access, pair.Value);
            }
        }
    }
}