using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Invokable;
using Sync.Value;

namespace Sync.Behaviour
{
    public static class ActionValidatorRegistry
    {
        public static bool TryGet(FieldId id, out IActionValidator validator)
        {
            return m_FieldValidators.TryGetValue(id, out validator);
        }

        public static bool TryGet(InvokableId id, out IActionValidator validator)
        {
            return m_MethodValidators.TryGetValue(id, out validator);
        }

        public static void Register(InvokableId id, [NotNull] IActionValidator validator)
        {
            lock (Lock)
            {
                if (m_MethodValidators.ContainsKey(id))
                    throw new ArgumentException($"Cannot register 2 validators for: {id}");

                m_MethodValidators.Add(id, validator);
            }
        }

        public static void Register(FieldId id, [NotNull] IActionValidator validator)
        {
            lock (Lock)
            {
                if (m_FieldValidators.ContainsKey(id))
                    throw new ArgumentException($"Cannot register 2 validators for: {id}");

                m_FieldValidators.Add(id, validator);
            }
        }

        #region Private

        private static readonly object Lock = new object();

        private static readonly Dictionary<InvokableId, IActionValidator> m_MethodValidators =
            new Dictionary<InvokableId, IActionValidator>();

        private static readonly Dictionary<FieldId, IActionValidator> m_FieldValidators =
            new Dictionary<FieldId, IActionValidator>();

        #endregion
    }
}