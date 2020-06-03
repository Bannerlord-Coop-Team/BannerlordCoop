using System;
using System.Collections.Generic;
using HarmonyLib;
using Sync.Reflection;

namespace Sync
{
    public static class FieldChangeBuffer
    {
        private static readonly Stack<ValueData> ActiveFields = new Stack<ValueData>();

        private static readonly HarmonyMethod PatchPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldChangeBuffer), nameof(PushActiveFields)))
        {
            priority = SyncPriority.SyncValuePre
        };

        private static readonly HarmonyMethod PatchPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldChangeBuffer), nameof(PopActiveFields)))
        {
            priority = SyncPriority.SyncValuePost
        };

        public static Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>
            BufferedChanges { get; } =
            new Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>>();

        private static void PushActiveFields()
        {
            ActiveFields.Push(null);
        }

        private static void OnBeforeExpectedChange(this ValueAccess access, object target)
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

        public static void TrackChanges(
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
                                value.OnBeforeExpectedChange(instance);
                            }
                        });
                }
            }
        }

        private static void PopActiveFields()
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
                bool changed = !Equals(newValue, data.Value);

                Dictionary<object, ValueChangeRequest> fieldBuffer = BufferedChanges.Assert(field);
                if (fieldBuffer.TryGetValue(data.Target, out ValueChangeRequest cached))
                {
                    if (changed && cached.RequestProcessed)
                    {
                        cached.RequestProcessed = false;
                    }

                    cached.RequestedValue = newValue;
                    field.Set(data.Target, cached.LatestActualValue);
                    continue;
                }

                if (!changed) continue;

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
