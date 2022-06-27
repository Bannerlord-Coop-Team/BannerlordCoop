using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sync.Value
{
    public class FieldPatch
    {
        public Dictionary<Tuple<MethodBase, FieldInfo>, FieldPatch> FieldPatches { get; } =
            new Dictionary<Tuple<MethodBase, FieldInfo>, FieldPatch>();

        MethodInfo caller;
        MethodInfo interceptMethod;
        FieldInfo field;

        public FieldPatch(MethodInfo caller, FieldInfo field, MethodInfo intercept)
        {
            this.caller = caller;
            this.field = field;

            interceptMethod = intercept;
        }

        
    }
}
