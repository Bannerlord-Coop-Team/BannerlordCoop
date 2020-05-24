using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public class SyncFieldGroup<TTarget, TGroup> : SyncValue
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

        public override object Get(object target)
        {
            return GetTyped(target);
        }

        public override void Set(object target, object value)
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
