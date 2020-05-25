using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Sync.Reflection;

namespace Sync
{
    public static class FieldWatcher
    {
        private static readonly Stack<SyncableData> ActiveFields = new Stack<SyncableData>();

        private static readonly HarmonyMethod PatchPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldWatcher), nameof(Prefix)))
        {
            priority = SyncPriority.SyncValuePre
        };

        private static readonly HarmonyMethod PatchPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(FieldWatcher), nameof(Postfix)))
        {
            priority = SyncPriority.SyncValuePost
        };

        public static Dictionary<SyncValue, Dictionary<object, BufferedData>>
            BufferedChanges { get; } =
            new Dictionary<SyncValue, Dictionary<object, BufferedData>>();

        private static void Prefix()
        {
            ActiveFields.Push(null);
        }

        /// <summary>
        ///     To be called before changing a syncable in a patched method.
        /// </summary>
        /// <param name="syncable"></param>
        /// <param name="target"></param>
        public static void Watch(this SyncValue syncable, object target)
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

        internal static void Patch(Harmony harmony, MethodBase method, HarmonyMethod patch)
        {
            harmony.Patch(method, patch, PatchPostfix);
            harmony.Patch(method, PatchPrefix, PatchPostfix);
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

                SyncValue field = data.Syncable;

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
