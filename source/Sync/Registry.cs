using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public static class Registry
    {
        public static IReadOnlyDictionary<MethodId, MethodAccess> IdToMethod => m_IdToMethod;
        public static IReadOnlyDictionary<ValueId, ValueAccess> IdToValue => m_IdToValue;
        public static IReadOnlyDictionary<MethodId, List<ValueId>> Relation => m_MethodValueRelation;

        public static MethodId Register([NotNull] MethodAccess methodAccess)
        {
            lock (Lock)
            {
                if (m_MethodToId.ContainsKey(methodAccess))
                {
                    throw new ArgumentException($"Duplicate register for: {methodAccess}");
                }

                MethodId id = MethodId.GetNextId();
                m_IdToMethod.Add(id, methodAccess);
                m_MethodToId.Add(methodAccess, id);
                return id;
            }
        }

        public static ValueId Register([NotNull] ValueAccess valueAccess)
        {
            lock (Lock)
            {
                if (m_ValueToId.ContainsKey(valueAccess))
                {
                    throw new ArgumentException($"Duplicate register for: {valueAccess}");
                }

                ValueId id = ValueId.GetNextId();
                m_IdToValue.Add(id, valueAccess);
                m_ValueToId.Add(valueAccess, id);
                return id;
            }
        }

        public static void AddRelation(MethodId method, ValueId value)
        {
            if (!m_MethodValueRelation.ContainsKey(method))
            {
                m_MethodValueRelation[method] = new List<ValueId>();
            }
            else if (m_MethodValueRelation[method].Contains(value))
            {
                return;
            }

            m_MethodValueRelation[method].Add(value);
        }
        
        #region Private
        private static readonly object Lock = new object();

        private static readonly Dictionary<MethodAccess, MethodId> m_MethodToId =
            new Dictionary<MethodAccess, MethodId>();

        private static readonly Dictionary<MethodId, MethodAccess> m_IdToMethod =
            new Dictionary<MethodId, MethodAccess>();
        
        private static readonly Dictionary<ValueAccess, ValueId> m_ValueToId =
            new Dictionary<ValueAccess, ValueId>();

        private static readonly Dictionary<ValueId, ValueAccess> m_IdToValue =
            new Dictionary<ValueId, ValueAccess>();

        private static readonly Dictionary<MethodId, List<ValueId>> m_MethodValueRelation =
            new Dictionary<MethodId, List<ValueId>>();

        #endregion
    }
}
