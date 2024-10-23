using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.Models
{
    public class ServiceTypeInfo
    {
        public ServiceTypeInfo(Type type)
        {
            this.Type = type;
        }

        public Type Type { get; set; }
        public List<PropertyInfo> Properties { get; set; } = new();
        public List<FieldInfo> Fields { get; set; } = new();
        public List<MemberInfo> Collections { get; set; } = new();
    }
}
