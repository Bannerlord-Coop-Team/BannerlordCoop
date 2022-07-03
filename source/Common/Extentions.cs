using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class Extentions
    {
        public static bool IsNative(this FieldInfo field)
        {
            Type type = field.FieldType;
            return (type != typeof(object) && Type.GetTypeCode(type) == TypeCode.Object);
        }
    }
}
