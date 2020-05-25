using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    public static class MethodRegistry
    {
        private static readonly Dictionary<SyncMethod, int> MethodIds =
            new Dictionary<SyncMethod, int>();

        private static readonly Dictionary<int, SyncMethod> Ids = new Dictionary<int, SyncMethod>();

        private static readonly Dictionary<Type, SyncMethod> Types =
            new Dictionary<Type, SyncMethod>();

        private static int _nextId;

        private static readonly HarmonyMethod PatchPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(MethodRegistry), nameof(Prefix)))
        {
            priority = SyncPriority.SyncCallPre
        };

        private static readonly HarmonyMethod PatchPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(MethodRegistry), nameof(Postfix)))
        {
            priority = SyncPriority.SyncCallPost
        };

        public static IReadOnlyDictionary<SyncMethod, int> MethodToId => MethodIds;
        public static IReadOnlyDictionary<Type, SyncMethod> TypeToSyncMethod => Types;

        public static void Register([NotNull] SyncMethod method)
        {
            if (MethodIds.ContainsKey(method))
            {
                throw new ArgumentException($"Duplicate register for: {method}");
            }

            int id = MakeId();
            Ids.Add(id, method);
            MethodIds.Add(method, id);
        }

        private static int MakeId()
        {
            return _nextId++;
        }

        private static bool Prefix()
        {
            return false;
        }

        private static void Postfix()
        {
        }

        internal static void Patch(Harmony harmony, MethodBase method, HarmonyMethod patch)
        {
            // Intended prefix order:
            // 1. Code in [SyncCall]. Supposed to call `RequestCall`.
            // 2. return false to prevent the actual call.
            patch.priority = SyncPriority.SyncCallPreUserPatch;
            harmony.Patch(method, PatchPrefix);
            harmony.Patch(method, patch);
        }

        public static void RequestCall(this SyncMethod sync, object instance, object[] args)
        {
            sync.GetSyncHandler(instance)?.Invoke(args);
        }
    }
}
