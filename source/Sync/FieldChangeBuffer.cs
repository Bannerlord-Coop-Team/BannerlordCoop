using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sync.Reflection;

namespace Sync
{
    public class FieldChangeBuffer
    {
        [NotNull]
        public object AddChange(ValueAccess access, FieldData data, object newValue)
        {
            lock (m_BufferedChanges)
            {
                Dictionary<object, ValueChangeRequest> fieldBuffer = m_BufferedChanges.Assert(access);
                if (fieldBuffer.TryGetValue(data.Target, out ValueChangeRequest cached))
                {
                    cached.RequestedValue = newValue;
                    return cached.OriginalValue;
                }

                fieldBuffer[data.Target] = new ValueChangeRequest
                {
                    OriginalValue = data.Value,
                    RequestedValue = newValue
                };
            }
            
            return data.Value;
        }

        [NotNull] public Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>> FetchChanges()
        {
            lock (m_BufferedChanges)
            {
                var ret = m_BufferedChanges;
                m_BufferedChanges = new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();
                return ret;
            }
        }

        public void AddChanges(FieldChangeBuffer other)
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
                        if (!changes.ContainsKey(entryInner.Key))
                        {
                            changes[entryInner.Key] = entryInner.Value;
                        }

                        changes[entryInner.Key].RequestedValue = entryInner.Value.RequestedValue;
                    }
                }
            }
        }

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
        
        private Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>> m_BufferedChanges =
            new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();
        #endregion
    }
}