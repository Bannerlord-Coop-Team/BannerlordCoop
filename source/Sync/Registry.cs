using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Call;
using Sync.Value;

namespace Sync
{
    /// <summary>
    ///     Central registry for fields & methods calls known to <see cref="Sync"/>. These are commonly referred to as
    ///     Actions. Actions are registered on creation of <see cref="Invokable"/> or <see cref="FieldBase"/>.
    /// </summary>
    public static class Registry
    {
        /// <summary>
        ///     Mapping from <see cref="InvokableId"/> to the associated <see cref="Invokable"/>.
        /// </summary>
        public static IReadOnlyDictionary<InvokableId, Invokable> IdToInvokable => m_IdToInvokable;
        /// <summary>
        ///     Mapping from <see cref="FieldId"/> to the associated <see cref="FieldBase"/>.
        /// </summary>
        public static IReadOnlyDictionary<FieldId, FieldBase> IdToField => m_IdToField;
        /// <summary>
        ///     Mapping from a <see cref="InvokableId"/> to a list of all related <see cref="FieldId"/>. This can,
        ///     for example, represent the relation from a property getter to the underlying field. Currently used
        ///     solely for debugging purposes.
        /// </summary>
        public static IReadOnlyDictionary<InvokableId, List<FieldId>> Relation => m_InvokableValueRelation;
        /// <summary>
        ///     Registers an invokable.
        /// </summary>
        /// <param name="invokable"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static InvokableId Register([NotNull] Invokable invokable)
        {
            lock (Lock)
            {
                if (m_MethodToId.ContainsKey(invokable))
                    throw new ArgumentException($"Duplicate register for: {invokable}");

                var id = InvokableId.CreateUnique();
                m_IdToInvokable.Add(id, invokable);
                m_MethodToId.Add(invokable, id);
                return id;
            }
        }
        /// <summary>
        ///     Registers a field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static FieldId Register([NotNull] FieldBase field)
        {
            lock (Lock)
            {
                if (m_ValueToId.ContainsKey(field)) throw new ArgumentException($"Duplicate register for: {field}");

                var id = FieldId.CreateUnique();
                m_IdToField.Add(id, field);
                m_ValueToId.Add(field, id);
                return id;
            }
        }
        /// <summary>
        ///     Creates a relation between an invokable and a field.
        /// </summary>
        /// <param name="invokable"></param>
        /// <param name="field"></param>
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

        private static readonly Dictionary<FieldBase, FieldId> m_ValueToId =
            new Dictionary<FieldBase, FieldId>();

        private static readonly Dictionary<FieldId, FieldBase> m_IdToField =
            new Dictionary<FieldId, FieldBase>();

        private static readonly Dictionary<InvokableId, List<FieldId>> m_InvokableValueRelation =
            new Dictionary<InvokableId, List<FieldId>>();

        #endregion
    }
}