using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sync.Reflection;

namespace Sync
{
    public static class Patcher
    {
        public static void ApplyPatch(Harmony harmony, Type type)
        {
            // [SyncWatch]
            foreach (MethodInfo toPatch in type.GetDeclaredMethods())
            {
                HarmonyMethod patch = new HarmonyMethod(toPatch);
                foreach (SyncWatchAttribute attr in
                    toPatch.GetCustomAttributes<SyncWatchAttribute>())
                {
                    FieldWatcher.Patch(harmony, attr.Method, patch);
                }
            }
        }
    }
}
