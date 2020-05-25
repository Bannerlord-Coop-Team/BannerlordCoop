using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sync
{
    public class SyncFieldGroup<TTarget, TGroup> : SyncValue
        where TGroup : class, IEnumerable<object>
    {
        private readonly IEnumerable<SyncField> m_Fields;

        public SyncFieldGroup(IEnumerable<SyncField> fields)
        {
            m_Fields = fields;
            ConstructorInfo constructor =
                typeof(TGroup).GetConstructor(new[] {typeof(IEnumerable<object>[])});
            if (constructor == null)
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
                                          ?.Invoke(
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
