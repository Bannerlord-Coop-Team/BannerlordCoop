using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization
{
    public class DynamicModelGenerator
    {
        public static void CreateDynamicSerializer<T>(IEnumerable<Type> exclude = null)
        {
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if(exclude != null)
            {
                HashSet<Type> excludesSet = exclude.ToHashSet();

                fields = fields.Where(f => excludesSet.Contains(f.FieldType) == false).ToArray();
            }

            string[] fieldNames = fields.Select(f => f.Name).ToArray();
            RuntimeTypeModel.Default.Add(typeof(T), true).Add(fieldNames);
        }

        public static void AssignSurrogate<T1, T2>()
        {
            RuntimeTypeModel.Default.Add(typeof(T1), false).SetSurrogate(typeof(T2));
        }

        public static void Compile()
        {
            RuntimeTypeModel.Default.CompileInPlace();
        }
    }
}
