using System.Collections.Generic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Represents a serializable call to a method including all invocation arguments. Method
    ///     pointer, instance and arguments have to resolved before execution. In order to resolve
    ///     a method call refer to <see cref="MethodRegistry" /> and <see cref="ArgumentFactory" />.
    /// </summary>
    public class MethodCall
    {
        public List<Argument> Arguments = new List<Argument>();

        public MethodId Id = MethodId.Invalid;
        public Argument Instance = Argument.Null; // Instance to call the method on.

        public override string ToString()
        {
            string sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (MethodRegistry.IdToMethod.TryGetValue(Id, out MethodAccess method))
            {
                sRet += $"{method}";
            }
            else
            {
                sRet += $"[UNREGISTRED] {Id.InternalValue}";
            }

            sRet += "(" + string.Join(", ", Arguments) + ")";
            return sRet;
        }
    }
}
