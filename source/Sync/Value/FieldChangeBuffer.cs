using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync.Value
{
    /// <summary>
    ///     A buffer of changes to a <see cref="FieldBase"/>.
    /// </summary>
    public class FieldChangeBuffer
    {
        /// <summary>
        ///     Adds a value change to a field.
        /// </summary>
        /// <param name="field">The field being changed</param>
        /// <param name="data">The associated instance data of the field</param>
        /// <param name="newValue">The new value of the field</param>
        /// <returns></returns>
        [NotNull]
        public object AddChange(FieldBase field, FieldData data, object newValue)
        {
            lock (m_BufferedChanges)
            {
                var fieldBuffer = m_BufferedChanges.Assert(field);
                if (fieldBuffer.TryGetValue(data.Target, out var cached))
                {
                    cached.RequestedValue = newValue;
                    return cached.OriginalValue;
                }

                fieldBuffer[data.Target] = new FieldChangeRequest
                {
                    OriginalValue = data.Value,
                    RequestedValue = newValue
                };
            }

            return data.Value;
        }
        /// <summary>
        ///     Returns all buffered changes and clears the buffer.
        /// </summary>
        /// <returns></returns>
        [NotNull]
        public Dictionary<FieldBase, Dictionary<object, FieldChangeRequest>> FetchChanges()
        {
            lock (m_BufferedChanges)
            {
                var ret = m_BufferedChanges;
                m_BufferedChanges = new Dictionary<FieldBase, Dictionary<object, FieldChangeRequest>>();
                return ret;
            }
        }
        /// <summary>
        ///     Merges the content of the given buffer into this.
        /// </summary>
        /// <param name="other"></param>
        public void Merge(FieldChangeBuffer other)
        {
            var changesOther = other.FetchChanges();
            lock (m_BufferedChanges)
            {
                foreach (var entry in changesOther)
                {
                    if (!m_BufferedChanges.ContainsKey(entry.Key))
                    {
                        m_BufferedChanges[entry.Key] = entry.Value;
                        continue;
                    }

                    var changes = m_BufferedChanges[entry.Key];
                    foreach (var entryInner in entry.Value)
                    {
                        if (!changes.ContainsKey(entryInner.Key)) changes[entryInner.Key] = entryInner.Value;

                        changes[entryInner.Key].RequestedValue = entryInner.Value.RequestedValue;
                    }
                }
            }
        }
        /// <summary>
        ///     Returns the number of buffered changes.
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            lock (m_BufferedChanges)
            {
                return m_BufferedChanges.Count;
            }
        }

        #region Private

        private static readonly Lazy<FieldChangeBuffer> m_Instance =
            new Lazy<FieldChangeBuffer>(() => new FieldChangeBuffer());

        private Dictionary<FieldBase, Dictionary<object, FieldChangeRequest>> m_BufferedChanges =
            new Dictionary<FieldBase, Dictionary<object, FieldChangeRequest>>();

        #endregion
    }
}