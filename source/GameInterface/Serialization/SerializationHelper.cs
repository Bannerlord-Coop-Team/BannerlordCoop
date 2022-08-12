using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization
{
    internal static class SerializationHelper
    {
        public static FieldInfo GetPrivateField<T>(string name)
        {
            return typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static PropertyInfo GetPrivateProperty<T>(string name)
        {
            return typeof(T).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static void AssignAsObjectType<T>(ref T toObject, FieldInfo field, object fromObject)
        {
            object memberObj = field.GetValue(fromObject);
            toObject = (T)memberObj;
        }
    }
}
