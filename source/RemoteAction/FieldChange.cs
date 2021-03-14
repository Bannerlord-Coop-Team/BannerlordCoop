using System.Collections.Generic;
using System.Linq;
using Sync;
using Sync.Behaviour;
using Sync.Value;

namespace RemoteAction
{
    /// <summary>
    ///     Represent a serializable change of a field value.
    /// </summary>
    public readonly struct FieldChange : ISynchronizedAction
    {
        /// <summary>
        ///     The id of the field that is changed.
        /// </summary>
        public readonly FieldId Id;
        /// <summary>
        ///     Instance containing the field.
        /// </summary>
        public readonly Argument Instance;
        /// <summary>
        ///     Arguments to the call. Always has length 1 and contains the new value of the field.
        /// </summary>
        public IEnumerable<Argument> Arguments { get; }
        /// <summary>
        ///     Evaluates whether this field change is valid.
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return ActionValidator.IsAllowed(Id);
        }
        public FieldChange(FieldId id, Argument instance, Argument value)
        {
            Id = id;
            Instance = instance;
            Arguments = new List<Argument> {value};
        }

        public override string ToString()
        {
            var sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (Registry.IdToField.TryGetValue(Id, out var field))
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