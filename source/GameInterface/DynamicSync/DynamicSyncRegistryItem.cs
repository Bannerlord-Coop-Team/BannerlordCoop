using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistryItem
    {
        public HashSet<FieldInfo> Fields = new HashSet<FieldInfo>();
        public HashSet<PropertyInfo> Properties = new HashSet<PropertyInfo>();

        public List<MethodInfo> TargetMethods = new List<MethodInfo>();

        public DynamicSyncRegistryItem()
        {
        }
    }
}
