using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public static class MethodRegistry
    {
        private static readonly object Lock = new object();

        private static readonly Dictionary<MethodAccess, MethodId> MethodIds =
            new Dictionary<MethodAccess, MethodId>();

        private static readonly Dictionary<MethodId, MethodAccess> Ids =
            new Dictionary<MethodId, MethodAccess>();

        private static readonly Dictionary<MethodBase, MethodAccess> Methods =
            new Dictionary<MethodBase, MethodAccess>();

        public static IReadOnlyDictionary<MethodAccess, MethodId> MethodToId => MethodIds;
        public static IReadOnlyDictionary<MethodId, MethodAccess> IdToMethod => Ids;

        public static MethodId Register([NotNull] MethodAccess methodAccess)
        {
            lock (Lock)
            {
                if (MethodIds.ContainsKey(methodAccess) || Methods.ContainsKey(methodAccess.MethodBase))
                {
                    throw new ArgumentException($"Duplicate register for: {methodAccess}");
                }

                MethodId id = MethodId.GetNextId();
                Ids.Add(id, methodAccess);
                MethodIds.Add(methodAccess, id);
                return id;
            }
        }
    }
}
