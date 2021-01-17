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

        private readonly HarmonyMethod PatchPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldChangeBuffer), nameof(PushActiveFields)))
        {
            priority = SyncPriority.FieldWatcherPre
        };

        private readonly HarmonyMethod PatchPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldChangeBuffer), nameof(PopActiveFields)))
        {
            priority = SyncPriority.FieldWatcherPost
        };

        public Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>
            BufferedChanges { get; } =
            new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();

        private void PushActiveFields()
        {
            ActiveFields.Push(null);
        }

        private void OnBeforeExpectedChange(ValueAccess access, object target)
        {
            object value;
            if (BufferedChanges.ContainsKey(access) &&
                BufferedChanges[access].TryGetValue(target, out ValueChangeRequest cache))
            {
                value = cache.RequestedValue;
                access.Set(target, value);
            }
            else
            {
                value = access.Get(target);
            }

            ActiveFields.Push(new ValueData(access, target, value));
        }

        /// <summary>
        ///     During the execution of any of the <paramref name="triggers" /> methods, any change to the
        ///     value will not be applied but instead written to the change buffer
        ///     <see cref="BufferedChanges" />.
        /// </summary>
        /// <param name="value">Accessor to the value</param>
        /// <param name="triggers">
        ///     Trigger methods during which the changes to the value are written to the
        ///     buffer instead
        /// </param>
        /// <param name="condition">The buffer is only active if the condition evaluates to true</param>
        public void Intercept(
            ValueAccess value,
            IEnumerable<MethodAccess> triggers,
            Func<bool> condition)
        {
            lock (Patcher.HarmonyLock)
            {
                foreach (MethodAccess method in triggers)
                {
                    Patcher.HarmonyInstance.Patch(method.MemberInfo, PatchPrefix, PatchPostfix);
                    method.SetGlobalHandler(
                        (instance, args) =>
                        {
                            if (condition())
                            {
                                OnBeforeExpectedChange(value, instance);
                            }

                            return true;
                        });
                }
            }
        }

        private void PopActiveFields()
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
                field.Set(data.Target, data.Value);
            }
        }
    }
}
