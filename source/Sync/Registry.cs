using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public static class Registry
    {
        public static IReadOnlyDictionary<MethodId, MethodAccess> IdToMethod => m_IdToMethod;
        public static IReadOnlyDictionary<FieldId, FieldAccess> IdToField => m_IdToField;
        public static IReadOnlyDictionary<MethodId, List<FieldId>> Relation => m_MethodFieldRelation;

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

        public static FieldId Register([NotNull] FieldAccess fieldAccess)
        {
            lock (Lock)
            {
                if (m_FieldToId.ContainsKey(fieldAccess))
                {
                    throw new ArgumentException($"Duplicate register for: {fieldAccess}");
                }

                FieldId id = FieldId.GetNextId();
                m_IdToField.Add(id, fieldAccess);
                m_FieldToId.Add(fieldAccess, id);
                return id;
            }
        }

        public static void AddRelation(MethodId method, FieldId field)
        {
            if (!m_MethodFieldRelation.ContainsKey(method))
            {
                m_MethodFieldRelation[method] = new List<FieldId>();
            }
            else if (m_MethodFieldRelation[method].Contains(field))
            {
                return;
            }

            m_MethodFieldRelation[method].Add(field);
        }
        
        #region Private
        private static readonly object Lock = new object();

        private static readonly Dictionary<MethodAccess, MethodId> m_MethodToId =
            new Dictionary<MethodAccess, MethodId>();

        private static readonly Dictionary<MethodId, MethodAccess> m_IdToMethod =
            new Dictionary<MethodId, MethodAccess>();
        
        private static readonly Dictionary<FieldAccess, FieldId> m_FieldToId =
            new Dictionary<FieldAccess, FieldId>();

        private static readonly Dictionary<FieldId, FieldAccess> m_IdToField =
            new Dictionary<FieldId, FieldAccess>();

        private static readonly Dictionary<MethodId, List<FieldId>> m_MethodFieldRelation =
            new Dictionary<MethodId, List<FieldId>>();

        #endregion
    }
}
