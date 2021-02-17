using System.Collections.Generic;
using System.Linq;
using Sync;
using Sync.Behaviour;

namespace RemoteAction
{
    /// <summary>
    ///     Represent a serializable change of a field value.
    /// </summary>
    public readonly struct FieldChange : ISynchronizedAction
    {
        public readonly ValueId Id;
        public readonly Argument Instance; // Instance of the object containing the field.
        public IEnumerable<Argument> Arguments { get; } // Length == 0. The new value of the field.

        public bool IsValid()
        {
            return ActionValidator.IsAllowed(Id);
        }

        public FieldChange(ValueId id, Argument instance, Argument value)
        {
            Id = id;
            Instance = instance;
            Arguments = new List<Argument> {value};
        }

        public override string ToString()
        {
            var sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (Registry.IdToValue.TryGetValue(Id, out var field))
                sRet += $"{field}";
            else
                sRet += $"[UNREGISTERED] {Id.InternalValue}";

            sRet += " = " + Arguments.First();
            return sRet;
        }

        public override bool Equals(object obj)
        {
            return obj is FieldChange field && Equals(field);
        }

        private bool Equals(FieldChange other)
        {
            return Equals(Arguments.First(), other.Arguments.First()) && Id.Equals(other.Id) &&
                   Instance.Equals(other.Instance);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ Arguments.First().GetHashCode();
                hashCode = (hashCode * 397) ^ Instance.GetHashCode();
                return hashCode;
            }
        }
    }
}