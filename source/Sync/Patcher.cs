using System;
using System.Reflection;
using HarmonyLib;
using Sync.Attributes;
using Sync.Reflection;

namespace Sync
{
    public static class Patcher
    {
        private static readonly Lazy<Harmony> m_HarmonyInstance =
            new Lazy<Harmony>(() => new Harmony("Sync.Patcher.Harmony"));

        public static Harmony HarmonyInstance => m_HarmonyInstance.Value;

        public static void ApplyPatch(Type type)
        {
            foreach (MethodInfo toPatch in type.GetDeclaredMethods())
            {
                HarmonyMethod patch = new HarmonyMethod(toPatch);
                // [SyncWatch]
                foreach (SyncWatchAttribute attr in
                    toPatch.GetCustomAttributes<SyncWatchAttribute>())
                {
                    if (!toPatch.IsStatic)
                    {
                        throw new Exception("Patch methods need to be static.");
                    }

                    FieldWatcher.Patch(HarmonyInstance, attr.Method, patch);
                }

                // [SyncCall]
                foreach (SyncCallAttribute attr in toPatch.GetCustomAttributes<SyncCallAttribute>())
                {
                    if (!toPatch.IsStatic)
                    {
                        throw new Exception("Patch methods need to be static.");
                    }

                    patch.priority = SyncPriority.SyncCallPreUserPatch;
                    HarmonyInstance.Patch(attr.Method, patch);
                }
            }
        }
    }
}
