using System;
using System.Collections.Generic;
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
                    if (cached.RequestProcessed)
                    {
                        cached.RequestProcessed = false;
                    }

                    cached.RequestedValue = newValue;
                    return cached.LatestActualValue;
                }

                fieldBuffer[data.Target] = new ValueChangeRequest
                {
                    LatestActualValue = data.Value,
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