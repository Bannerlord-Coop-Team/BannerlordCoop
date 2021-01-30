using System;
using System.Collections.Generic;
using HarmonyLib;
using Sync.Reflection;

namespace Sync
{
    /// <summary>
    ///     Buffer for pending changes to a value. The changes are detected by creating a snapshot
    ///     of the value in a prefix to the trigger function and restoring that snapshot in a
    ///     postfix to the trigger function.
    ///     The buffer <see cref="BufferedChanges" /> will never be cleared internally. The game
    ///     loop is responsible to process the buffered changes and apply or discard them.
    /// </summary>
    public class FieldChangeStack
    {
        /// <summary>
        ///     Buffer of all recorded changes to field values.
        /// </summary>
        public Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>> BufferedChanges { get; } =
            new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();

        /// <summary>
        ///     Pushes a marker to the stack. The next call to <see cref="PopUntilMarker"/> will pop until this marker
        ///     is encountered.
        /// </summary>
        public void PushMarker()
        {
            m_ActiveFields.Push(null);
        }

        /// <summary>
        ///     Pushes the current value of the given field to the stack.
        /// </summary>
        /// <param name="access">Access object to the field value</param>
        /// <param name="target">Instance that the field belongs to</param>
        public void PushValue(ValueAccess access, object target)
        {
            m_ActiveFields.Push(new ValueData(access, target, access.Get(target)));
        }

        /// <summary>
        ///     Pops all changes until a marker is encountered. The popped field changes are stored in the
        ///     <see cref="BufferedChanges"/>.
        /// </summary>
        /// <param name="bRevertToOriginalValue"></param>
        public void PopUntilMarker(bool bRevertToOriginalValue)
        {
            while (m_ActiveFields.Count > 0)
            {
                ValueData data = m_ActiveFields.Pop();
                if (data == null)
                {
                    break; // The marker
                }

                ValueAccess field = data.Access;

                object newValue = data.Access.Get(data.Target);
                bool changed = !newValue.Equals(data.Value);

                if (!changed) continue;

                Dictionary<object, ValueChangeRequest> fieldBuffer = BufferedChanges.Assert(field);
                if (fieldBuffer.TryGetValue(data.Target, out ValueChangeRequest cached))
                {
                    if (cached.RequestProcessed)
                    {
                        cached.RequestProcessed = false;
                    }

                    cached.RequestedValue = newValue;
                    field.Set(data.Target, cached.LatestActualValue);
                    continue;
                }

                fieldBuffer[data.Target] = new ValueChangeRequest
                {
                    LatestActualValue = data.Value,
                    RequestedValue = newValue
                };

                if (bRevertToOriginalValue)
                {
                    field.Set(data.Target, data.Value);
                }
            }
        }
        
        #region Private
        private readonly Stack<ValueData> m_ActiveFields = new Stack<ValueData>();
        #endregion
    }
}
