using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync
{
    public static class MethodRegistry
    {
        private static readonly Dictionary<SyncMethod, MethodId> MethodIds =
            new Dictionary<SyncMethod, MethodId>();

        private static readonly Dictionary<MethodId, SyncMethod> Ids =
            new Dictionary<MethodId, SyncMethod>();

        private static readonly Dictionary<Type, SyncMethod> Types =
            new Dictionary<Type, SyncMethod>();

        public static IReadOnlyDictionary<SyncMethod, MethodId> MethodToId => MethodIds;
        public static IReadOnlyDictionary<MethodId, SyncMethod> IdToMethod => Ids;
        public static IReadOnlyDictionary<Type, SyncMethod> TypeToSyncMethod => Types;

        public static MethodId Register([NotNull] SyncMethod method)
        {
            if (MethodIds.ContainsKey(method))
            {
                throw new ArgumentException($"Duplicate register for: {method}");
            }

            MethodId id = MethodId.GetNextId();
            Ids.Add(id, method);
            MethodIds.Add(method, id);
            return id;
        }

        public static bool RequestCall(
            this SyncMethod sync,
            [CanBeNull] object instance,
            params object[] args)
        {
            Action<object> handler = sync.GetHandler(instance);
            handler?.Invoke(args);
            return handler == null;
        }
    }
}
