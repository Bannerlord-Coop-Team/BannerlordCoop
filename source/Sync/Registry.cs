using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Call;
using Sync.Value;

namespace Sync
{
    public static class Registry
    {
        public static IReadOnlyDictionary<InvokableId, Call.Invokable> IdToInvokable => m_IdToInvokable;
        public static IReadOnlyDictionary<FieldId, Field> IdToField => m_IdToField;
        public static IReadOnlyDictionary<InvokableId, List<FieldId>> Relation => m_InvokableValueRelation;

        public static InvokableId Register([NotNull] Call.Invokable invokable)
        {
            lock (Lock)
            {
                if (m_MethodToId.ContainsKey(invokable))
                    throw new ArgumentException($"Duplicate register for: {invokable}");

                var id = InvokableId.GetNextId();
                m_IdToInvokable.Add(id, invokable);
                m_MethodToId.Add(invokable, id);
                return id;
            }
        }

        public static FieldId Register([NotNull] Field field)
        {
            lock (Lock)
            {
                if (m_ValueToId.ContainsKey(field)) throw new ArgumentException($"Duplicate register for: {field}");

                var id = FieldId.GetNextId();
                m_IdToField.Add(id, field);
                m_ValueToId.Add(field, id);
                return id;
            }
        }

        public static void AddRelation(InvokableId invokable, FieldId field)
        {
            if (!m_InvokableValueRelation.ContainsKey(invokable))
                m_InvokableValueRelation[invokable] = new List<FieldId>();
            else if (m_InvokableValueRelation[invokable].Contains(field)) return;

            m_InvokableValueRelation[invokable].Add(field);
        }

        #region Private

        private static readonly object Lock = new object();

        private static readonly Dictionary<Call.Invokable, InvokableId> m_MethodToId =
            new Dictionary<Call.Invokable, InvokableId>();

        private static readonly Dictionary<InvokableId, Call.Invokable> m_IdToInvokable =
            new Dictionary<InvokableId, Call.Invokable>();

        private static readonly Dictionary<Field, FieldId> m_ValueToId =
            new Dictionary<Field, FieldId>();

        private static readonly Dictionary<FieldId, Field> m_IdToField =
            new Dictionary<FieldId, Field>();

        private static readonly Dictionary<InvokableId, List<FieldId>> m_InvokableValueRelation =
            new Dictionary<InvokableId, List<FieldId>>();

        #endregion
    }
}