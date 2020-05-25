using System;
using System.Reflection;
using HarmonyLib;
using Sync.Attributes;
using Sync.Reflection;

namespace Sync
{
    public static class Patcher
    {
        public static void ApplyPatch(Harmony harmony, Type type)
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

                    FieldWatcher.Patch(harmony, attr.Method, patch);
                }

                // [SyncCall]
                foreach (SyncCallAttribute attr in toPatch.GetCustomAttributes<SyncCallAttribute>())
                {
                    if (!toPatch.IsStatic)
                    {
                        throw new Exception("Patch methods need to be static.");
                    }

                    MethodRegistry.Patch(harmony, attr.Method, patch);
                }
            }
        }
    }
}
