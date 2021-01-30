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
    public class FieldChangeBuffer
    {
        private readonly Stack<ValueData> ActiveFields = new Stack<ValueData>();

        public Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>
            BufferedChanges { get; } =
            new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();

        public void PushActiveFieldMarker()
        {
            ActiveFields.Push(null);
        }

        public void PushActiveField(ValueAccess access, object target)
        {
            ActiveFields.Push(new ValueData(access, target, access.Get(target)));
        }

        public void PopActiveFields(bool bRevertToOriginalValue)
        {
            while (ActiveFields.Count > 0)
            {
                ValueData data = ActiveFields.Pop();
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
    }
}
