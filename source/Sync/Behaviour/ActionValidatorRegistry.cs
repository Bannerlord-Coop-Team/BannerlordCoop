using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Call;
using Sync.Value;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Registry for action validators. For internal use only, public access through <see cref="ActionValidator"/>.
    /// </summary>
    internal static class ActionValidatorRegistry
    {
        /// <summary>
        ///     Returns the validator for the given field (if one exists).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <returns></returns>
        public static bool TryGet(FieldId id, out IActionValidator validator)
        {
            return m_FieldValidators.TryGetValue(id, out validator);
        }
        /// <summary>
        ///     Returns the validator for the given invokable (if one exists).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <returns></returns>
        public static bool TryGet(InvokableId id, out IActionValidator validator)
        {
            return m_MethodValidators.TryGetValue(id, out validator);
        }
        /// <summary>
        ///     Registers a validator for a invokable.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void Register(InvokableId id, IActionValidator validator)
        {
            lock (Lock)
            {
                if (m_MethodValidators.ContainsKey(id))
                    throw new ArgumentException($"Cannot register 2 validators for: {id}");

                m_MethodValidators.Add(id, validator);
            }
        }
        /// <summary>
        ///     Registers a validator for a field.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="validator"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void Register(FieldId id, IActionValidator validator)
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