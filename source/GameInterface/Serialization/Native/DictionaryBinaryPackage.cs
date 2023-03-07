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

        readonly Type enumerableType;

        IBinaryPackage[] packages;

        public DictionaryBinaryPackage(IEnumerable enumerable, IBinaryPackageFactory packageFactory)
        {
            PackageFactory = packageFactory;
            this.enumerable = enumerable;
            enumerableType = enumerable.GetType();
        }

        public void Pack()
        {
            List<IBinaryPackage> binaryPackages = new List<IBinaryPackage>();
            foreach (var obj in enumerable)
            {
                binaryPackages.Add(PackageFactory.GetBinaryPackage(obj));
            }

            packages = binaryPackages.ToArray();
        }

        public object Unpack()
        {
            var unpackedArray = packages.Select(e => e.Unpack()).ToArray();

            var newDict = Activator.CreateInstance(enumerableType);

            MethodInfo DictAdd = enumerableType.GetMethod("Add");

            Type KeyType = enumerableType.GetGenericArguments()[0];
            Type ValueType = enumerableType.GetGenericArguments()[1];

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
