using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistryItem
    {
        public HashSet<MemberInfo> Members = new HashSet<MemberInfo>();

        public List<MethodInfo> TargetMethods = new List<MethodInfo>();

        public DynamicSyncRegistryItem()
        {
        }
    }
}
