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
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetUnderlyingType() == typeof(MethodAccess) &&
                    field.GetValue(null) is MethodAccess method)
                {
                    m_Handlers.Add(new MethodCallSyncHandler(method));
                }
                else if (field.GetUnderlyingType() == typeof(MethodPatcher) &&
                         field.GetValue(null) is MethodPatcher patcher)
                {
                    foreach (MethodAccess syncMethod in patcher.Methods)
                    {
                        m_Handlers.Add(new MethodCallSyncHandler(syncMethod));
                    }
                }
            }
        }
    }
}
