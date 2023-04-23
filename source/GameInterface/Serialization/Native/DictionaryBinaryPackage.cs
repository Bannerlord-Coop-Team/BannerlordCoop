using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class DictionaryBinaryPackage : IEnumerableBinaryPackage
    {
        [NonSerialized]
        readonly IBinaryPackageFactory PackageFactory;

        [NonSerialized]
        readonly IEnumerable enumerable;

        readonly string enumerableType;

        IBinaryPackage[] packages;

        public DictionaryBinaryPackage(IEnumerable enumerable, IBinaryPackageFactory packageFactory)
        {
            PackageFactory = packageFactory;
            this.enumerable = enumerable;
            enumerableType = enumerable.GetType().AssemblyQualifiedName;
        }

        public void Pack()
        {
            List<IBinaryPackage> binaryPackages = new List<IBinaryPackage>();
            foreach (var obj in enumerable)
            {
                var package = PackageFactory.GetBinaryPackage(obj);
                binaryPackages.Add(package);
            }

            packages = binaryPackages.ToArray();
        }

        public object Unpack()
        {
            var unpackedArray = packages.Select(e => e.Unpack()).ToArray();
            var type = Type.GetType(enumerableType);
            var newDict = Activator.CreateInstance(type);

            MethodInfo DictAdd = type.GetMethod("Add");

            Type KeyType = type.GetGenericArguments()[0];
            Type ValueType = type.GetGenericArguments()[1];

            Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(KeyType, ValueType);

            FieldInfo Key = kvpType.GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo Value = kvpType.GetField("value", BindingFlags.Instance | BindingFlags.NonPublic);


            foreach (object obj in unpackedArray)
            {
                var k = Key.GetValue(obj);
                var v = Value.GetValue(obj);
                DictAdd.Invoke(newDict, new object[] { k, v });
            }

            return newDict;
        }

        public T Unpack<T>()
        {
            return (T)Unpack();
        }
    }
}
