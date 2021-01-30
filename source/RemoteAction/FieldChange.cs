using System.Collections.Generic;
using System.Linq;
using Sync;

namespace RemoteAction
{
    /// <summary>
    ///     Represent a serializable change of a field value.
    /// </summary>
    public readonly struct FieldChange : IAction
    {
        public readonly FieldId Id;
        public readonly Argument Instance; // Instance of the object containing the field.
        public IEnumerable<Argument> Arguments { get; } // Length == 0. The new value of the field.
        
        public FieldChange(FieldId id, Argument instance, Argument value)
        {
            Id = id;
            Instance = instance;
            Arguments = new List<Argument>() { value };
        }
        
        public override string ToString()
        {
            string sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (Registry.IdToField.TryGetValue(Id, out FieldAccess field))
            {
                sRet += $"{field}";
            }
            else
            {
                sRet += $"[UNREGISTERED] {Id.InternalValue}";
            }

            sRet += " = " + Arguments.First();
            return sRet;
        }

        public override bool Equals(object obj)
        {
            return obj is FieldChange field && this.Equals(field);
        }

        private bool Equals(FieldChange other)
        {
            return Equals(Arguments.First(), other.Arguments.First()) && Id.Equals(other.Id) && Instance.Equals(other.Instance);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ Arguments.First().GetHashCode();
                hashCode = (hashCode * 397) ^ Instance.GetHashCode();
                return hashCode;
            }
        }
    }
}