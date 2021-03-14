using System.Collections.Generic;
using Sync;
using Sync.Behaviour;
using Sync.Call;

namespace RemoteAction
{
    /// <summary>
    ///     Represents a serializable call to a method including all invocation arguments. Method
    ///     pointer, instance and arguments have to resolved before execution. In order to resolve
    ///     a method call refer to <see cref="Registry" /> and <see cref="ArgumentFactory" />.
    /// </summary>
    public readonly struct MethodCall : ISynchronizedAction
    {
        /// <summary>
        ///     Arguments to the method call.
        /// </summary>
        public IEnumerable<Argument> Arguments { get; }
        /// <summary>
        ///     Evaluates whether this method call is valid.
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return ActionValidator.IsAllowed(Id);
        }
        /// <summary>
        ///     The id of the method call.
        /// </summary>
        public readonly InvokableId Id;
        /// <summary>
        ///     Instance that the call was made on. Null for static.
        /// </summary>
        public readonly Argument Instance;

        public MethodCall(InvokableId id, Argument instance, IEnumerable<Argument> arguments)
        {
            Arguments = arguments;
            Id = id;
            Instance = instance;
        }

        public override string ToString()
        {
            var sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (Registry.IdToInvokable.TryGetValue(Id, out var method))
                sRet += $"{method}";
            else
                sRet += $"[UNREGISTERED] {Id.InternalValue}";

            sRet += "(" + string.Join(", ", Arguments) + ")";
            return sRet;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodCall call && Equals(call);
        }

        private bool Equals(MethodCall other)
        {
            return Equals(Arguments, other.Arguments) && Id.Equals(other.Id) && Instance.Equals(other.Instance);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                foreach (var argument in Arguments) hashCode = (hashCode * 397) ^ argument.GetHashCode();
                hashCode = (hashCode * 397) ^ Instance.GetHashCode();
                return hashCode;
            }
        }
    }
}