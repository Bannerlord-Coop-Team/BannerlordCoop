using System.Collections.Generic;
using RailgunNet.System.Types;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class MethodCall
    {
        public List<Argument> Arguments = new List<Argument>();

        public MethodId Id = MethodId.Invalid;
        public Argument Instance = Argument.Null; // Instance to call the method on.
        public static EntityId StaticContext => EntityId.INVALID;

        public override string ToString()
        {
            string sRet = Instance.EventType == EventArgType.Null ? "static " : $"{Instance} ";
            if (MethodRegistry.IdToMethod.TryGetValue(Id, out SyncMethod method))
            {
                sRet = $"{method}";
            }
            else
            {
                sRet = $"[UNREGISTRED] {Id.InternalValue}";
            }

            sRet += "(" + string.Join(", ", Arguments) + ")";
            return sRet;
        }
    }
}
