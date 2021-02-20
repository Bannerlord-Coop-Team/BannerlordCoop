using System.Collections.Generic;

namespace Sync.Value
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
        #region Private

        private readonly Stack<FieldData> m_ActiveFields = new Stack<FieldData>();

        #endregion

        /// <summary>
        ///     Pushes a marker to the stack. The next call to <see cref="PopUntilMarker" /> will pop until this marker
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
        public void PushValue(Field access, object target)
        {
            m_ActiveFields.Push(new FieldData(access, target, access.Get(target)));
        }

        /// <summary>
        ///     Pops all changes until a marker is encountered. The popped field changes are stored in the
        ///     <see cref="BufferedChanges" />.
        /// </summary>
        /// <param name="bRevertToOriginalValue"></param>
        public FieldChangeBuffer PopUntilMarker(bool bRevertToOriginalValue)
        {
            var buffer = new FieldChangeBuffer();
            while (m_ActiveFields.Count > 0)
            {
                var data = m_ActiveFields.Pop();
                if (data == null) break; // The marker

                var access = data.Access;

                var newValue = data.Access.Get(data.Target);
                var changed = !Equals(newValue, data.Value);

                if (!changed) continue;

                var latestActualValue = buffer.AddChange(access, data, newValue);
                if (bRevertToOriginalValue) access.Set(data.Target, latestActualValue);
            }

            return buffer;
        }
    }
}