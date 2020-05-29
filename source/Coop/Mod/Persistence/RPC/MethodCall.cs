using System.Collections.Generic;
using RailgunNet.System.Types;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class MethodCall
    {
        public List<Argument> Arguments = new List<Argument>();
        public Argument Instance = Argument.Null; // Instance to call the method on.

        public MethodId Method = MethodId.Invalid;
        public static EntityId StaticContext => EntityId.INVALID;

        public override string ToString()
        {
            string sRet = "";
            if (MethodRegistry.IdToMethod.TryGetValue(Method, out SyncMethod method))
            {
                sRet = $"Call {method}";
            }
            else
            {
                sRet = $"Unknown call {Method.InternalValue}";
            }

            sRet += "(" + string.Join(", ", Arguments) + ")";
            sRet += $" on {Instance}";
            return sRet;
        }
    }
}
