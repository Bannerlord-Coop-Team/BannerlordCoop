using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Sync;
using Sync.Attributes;

namespace Coop.Mod.Persistence.RPC
{
    public class RPCSyncHandlers
    {
        private readonly List<MethodCallSyncHandler> m_Handlers = new List<MethodCallSyncHandler>();

        public RPCSyncHandlers()
        {
            // Create a sync handler for every SyncMethod in our patches
            IEnumerable<Type> patches =
                from t in Assembly.GetExecutingAssembly().GetTypes()
                where t.IsDefined(typeof(PatchAttribute))
                select t;
            foreach (Type patch in patches)
            {
                Init(patch);
            }
        }

        public IReadOnlyList<MethodCallSyncHandler> Handlers => m_Handlers;

        private void Init(Type type)
        {
            foreach (SyncMethod method in type
                                          .GetFields(
                                              BindingFlags.Static |
                                              BindingFlags.Public |
                                              BindingFlags.NonPublic)
                                          .Where(f => f.GetUnderlyingType() == typeof(SyncMethod))
                                          .Select(f => f.GetValue(null) as SyncMethod))
            {
                m_Handlers.Add(new MethodCallSyncHandler(method));
            }
        }
    }
}
