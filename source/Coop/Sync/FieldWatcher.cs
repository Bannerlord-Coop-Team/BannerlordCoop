using System;
using System.Collections.Generic;
using System.Reflection;
using Coop.Game;
using Coop.Reflection;
using HarmonyLib;

namespace Coop.Sync
{
    public static class FieldWatcher
    {
        private static readonly Stack<SyncFieldData> ActiveFields = new Stack<SyncFieldData>();

        public static Dictionary<SyncField, Dictionary<object, BufferedData>> BufferedChanges { get; } =
            new Dictionary<SyncField, Dictionary<object, BufferedData>>();

        private static void Prefix()
        {
            ActiveFields.Push(null);
        }

        /// <summary>
        ///     To be called before changing fields in a patched method.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="target"></param>
        public static void Watch(this SyncField field, object target)
        {
            object value = null;
            if (BufferedChanges.ContainsKey(field) &&
                BufferedChanges[field].TryGetValue(target, out BufferedData cache))
            {
                value = cache.ToSend;
                field.Set(target, value);
            }
            else
            {
                value = field.Get(target);
            }

            ActiveFields.Push(new SyncFieldData(field, target, value));
        }

        private static void Postfix()
        {
            while (ActiveFields.Count > 0)
            {
                SyncFieldData data = ActiveFields.Pop();
                if (data == null)
                {
                    break; // The marker
                }

                SyncField field = data.Field;

                object newValue = data.Field.Get(data.Target);
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
