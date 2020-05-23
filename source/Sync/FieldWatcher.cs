using System;
using System.Collections.Generic;
using System.Reflection;
using Sync.Reflection;
using HarmonyLib;

namespace Sync
{
    public static class FieldWatcher
    {
        private static readonly Stack<SyncableData> ActiveFields = new Stack<SyncableData>();

        public static Dictionary<ISyncable, Dictionary<object, BufferedData>>
            BufferedChanges { get; } =
            new Dictionary<ISyncable, Dictionary<object, BufferedData>>();

        private static void Prefix()
        {
            ActiveFields.Push(null);
        }

        /// <summary>
        ///     To be called before changing a syncable in a patched method.
        /// </summary>
        /// <param name="syncable"></param>
        /// <param name="target"></param>
        public static void Watch(this ISyncable syncable, object target)
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

        private static void Postfix()
        {
            while (ActiveFields.Count > 0)
            {
                SyncableData data = ActiveFields.Pop();
                if (data == null)
                {
                    break; // The marker
                }

                ISyncable field = data.Syncable;

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

        public static void ApplyFieldWatcherPatches(Harmony harmony, Type type)
        {
            HarmonyMethod prefix = new HarmonyMethod(
                AccessTools.Method(typeof(FieldWatcher), nameof(Prefix)))
            {
                priority = SyncPriority.First
            };
            HarmonyMethod postfix = new HarmonyMethod(
                AccessTools.Method(typeof(FieldWatcher), nameof(Postfix)))
            {
                priority = SyncPriority.Last
            };

            foreach (MethodInfo toPatch in type.GetDeclaredMethods())
            {
                HarmonyMethod patch = new HarmonyMethod(toPatch);
                foreach (SyncWatchAttribute attr in
                    toPatch.GetCustomAttributes<SyncWatchAttribute>())
                {
                    harmony.Patch(attr.Method, patch, postfix);
                    harmony.Patch(attr.Method, prefix, postfix);
                }
            }
        }
    }
}
