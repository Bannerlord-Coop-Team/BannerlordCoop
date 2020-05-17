using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Coop.Sync
{
    public class SyncFieldGroup<TTarget, TGroup> : ISyncable
        where TGroup : IEnumerable<object>
    {
        private readonly ConstructorInfo m_Constructor;
        private readonly IEnumerable<SyncField> m_Fields;

        private readonly Dictionary<object, Action<object>> m_SyncHandlers =
            new Dictionary<object, Action<object>>();

        public SyncFieldGroup(IEnumerable<SyncField> fields)
        {
            m_Fields = fields;
            m_Constructor = typeof(TGroup).GetConstructor(new[] {typeof(IEnumerable<object>[])});
            if (m_Constructor == null)
            {
                throw new ArgumentException($"{typeof(TGroup)} has no matching constructor.");
            }
        }

        public void SetSyncHandler([NotNull] object syncableInstance, Action<object> action)
        {
            if (m_SyncHandlers.ContainsKey(syncableInstance))
            {
                throw new ArgumentException($"Cannot have multiple sync handlers for {this}.");
            }

            m_SyncHandlers.Add(syncableInstance, action);
        }

        public void RemoveSyncHandler([NotNull] object syncableInstance)
        {
            m_SyncHandlers.Remove(syncableInstance);
        }

        [CanBeNull]
        public Action<object> GetSyncHandler([NotNull] object syncableInstance)
        {
            return m_SyncHandlers.TryGetValue(syncableInstance, out Action<object> handler) ?
                handler :
                null;
        }

        public object Get(object target)
        {
            return GetTyped(target);
        }

        public void Set(object target, object value)
        {
            SetTyped((TTarget) target, (TGroup) value);
        }

        public TGroup GetTyped(object target)
        {
            return (TGroup) typeof(TGroup).GetConstructor(new[] {typeof(IEnumerable<object>[])})
                                          .Invoke(
                                              new object[] {m_Fields.Select(i => i.Get(target))});
        }

        public void SetTyped(TTarget target, TGroup group)
        {
            var zipped = m_Fields.Zip(
                group,
                (field, value) => new
                {
                    Field = field,
                    Value = value
                });
            foreach (var pair in zipped)
            {
                pair.Field.Set(target, pair.Value);
            }
        }
    }
}
