using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sync
{
    public static class MethodRegistry
    {
        private static readonly Dictionary<MethodAccess, MethodId> MethodIds =
            new Dictionary<MethodAccess, MethodId>();

        private static readonly Dictionary<MethodId, MethodAccess> Ids =
            new Dictionary<MethodId, MethodAccess>();

        private static readonly Dictionary<Type, MethodAccess> Types =
            new Dictionary<Type, MethodAccess>();

        public static IReadOnlyDictionary<MethodAccess, MethodId> MethodToId => MethodIds;
        public static IReadOnlyDictionary<MethodId, MethodAccess> IdToMethod => Ids;
        public static IReadOnlyDictionary<Type, MethodAccess> TypeToSyncMethod => Types;

        public static MethodId Register([NotNull] MethodAccess methodAccess)
        {
            if (MethodIds.ContainsKey(methodAccess))
            {
                throw new ArgumentException($"Duplicate register for: {methodAccess}");
            }

            MethodId id = MethodId.GetNextId();
            Ids.Add(id, methodAccess);
            MethodIds.Add(methodAccess, id);
            return id;
        }

        public static bool RequestCall(
            this MethodAccess sync,
            [CanBeNull] object instance,
            params object[] args)
        {
            Action<object> handler = sync.GetHandler(instance);
            handler?.Invoke(args);
            return handler == null;
        }
    }
}
