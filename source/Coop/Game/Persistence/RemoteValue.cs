using System;
using System.ComponentModel;
using System.Reflection;
using RailgunNet.Util;

namespace Coop.Game.Persistence
{
    public class RemoteValue<TValue>
        where TValue : struct
    {
        private readonly INotifyPropertyChanged m_BoundInstance;
        private readonly Func<TValue> m_Getter;
        private readonly string m_PropertyName;

        private TValue? m_RequestedValue;

        public RemoteValue(INotifyPropertyChanged instance, PropertyInfo property)
        {
            if (property.PropertyType != typeof(TValue))
            {
                throw new ArgumentException(
                    $"{property} cannot be bound to a RemoveValue<{typeof(TValue)}>: type mismatch.",
                    nameof(property));
            }

            m_BoundInstance = instance;
            m_Getter = InvokableFactory.CreateGetter<TValue>(property, instance);
            m_PropertyName = property.Name;
            m_BoundInstance.PropertyChanged += ValueChanged;
        }

        public event Action<TValue> OnValueChanged;

        private void ValueChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == m_PropertyName)
            {
                OnValueChanged?.Invoke(m_Getter());
            }
        }

        public void Request(TValue newValue)
        {
            m_RequestedValue = newValue;
        }

        public TValue? DrainRequest()
        {
            TValue? temp = m_RequestedValue;
            m_RequestedValue = null;
            return temp;
        }
    }
}
