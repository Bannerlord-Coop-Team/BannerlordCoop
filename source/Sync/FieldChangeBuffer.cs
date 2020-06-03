using System;
using System.Collections.Generic;
using HarmonyLib;
using Sync.Reflection;

namespace Sync
{
    public static class FieldChangeBuffer
    {
        private static readonly Stack<SyncableData> ActiveFields = new Stack<SyncableData>();

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

        public static Dictionary<ValueAccess, Dictionary<object, BufferedData>>
            BufferedChanges { get; } =
            new Dictionary<ValueAccess, Dictionary<object, BufferedData>>();

        private static void PushActiveFields()
        {
            ActiveFields.Push(null);
        }

        /// <summary>
        ///     To be called before changing a syncable in a patched method.
        /// </summary>
        /// <param name="syncable"></param>
        /// <param name="target"></param>
        private static void Watch(this ValueAccess syncable, object target)
        {
            object value = null;
            if (BufferedChanges.ContainsKey(syncable) &&
                BufferedChanges[syncable].TryGetValue(target, out BufferedData cache))
            {
                value = cache.ToSend;
                syncable.Set(target, value);
            }
            else
            {
                value = syncable.Get(target);
            }

            ActiveFields.Push(new SyncableData(syncable, target, value));
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
                                value.Watch(instance);
                            }
                        });
                }
            }
        }

        private static void PopActiveFields()
        {
            while (ActiveFields.Count > 0)
            {
                SyncableData data = ActiveFields.Pop();
                if (data == null)
                {
                    break; // The marker
                }

                ValueAccess field = data.Syncable;

                object newValue = data.Syncable.Get(data.Target);
                bool changed = !Equals(newValue, data.Value);

                Dictionary<object, BufferedData> fieldBuffer = BufferedChanges.Assert(field);
                if (fieldBuffer.TryGetValue(data.Target, out BufferedData cached))
                {
                    if (changed && cached.Sent)
                    {
                        cached.Sent = false;
                    }

                    cached.ToSend = newValue;
                    field.Set(data.Target, cached.Actual);
                    continue;
                }

                if (!changed) continue;

                fieldBuffer[data.Target] = new BufferedData
                {
                    Actual = data.Value,
                    ToSend = newValue
                };
                field.Set(data.Target, data.Value);
            }
        }
    }
}
