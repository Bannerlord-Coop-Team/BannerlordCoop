using Common.Logging;
using Serilog;
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
        private static ILogger Logger = LogManager.GetLogger<DictionaryBinaryPackage>();

        [NonSerialized]
        private IBinaryPackageFactory binaryPackageFactory;

        [NonSerialized]
        private readonly IEnumerable enumerable;

        readonly string enumerableType;

        IBinaryPackage[] packages;

        public DictionaryBinaryPackage(IEnumerable enumerable, IBinaryPackageFactory binaryPackageFactory)
        {
            this.binaryPackageFactory = binaryPackageFactory;
            this.enumerable = enumerable;
            enumerableType = enumerable.GetType().AssemblyQualifiedName;
        }

        public void Pack()
        {
            List<IBinaryPackage> binaryPackages = new List<IBinaryPackage>();
            foreach (var obj in enumerable)
            {
                binaryPackages.Add(binaryPackageFactory.GetBinaryPackage(obj));
            }

            packages = binaryPackages.ToArray();
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            this.binaryPackageFactory = binaryPackageFactory;

            var unpackedArray = packages.Select(e => e.Unpack(binaryPackageFactory)).ToArray();
            var type = Type.GetType(enumerableType);
            var newDict = Activator.CreateInstance(type);

            MethodInfo dictAdd = type.GetMethod("Add");

            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];

            Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);

            FieldInfo key = kvpType.GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo value = kvpType.GetField("value", BindingFlags.Instance | BindingFlags.NonPublic);


            foreach (object obj in unpackedArray)
            {
                var k = key.GetValue(obj);
                var v = value.GetValue(obj);

                if(k == null)
                {
                    Logger.Warning("Key was null while unpacking dictionary");
                    continue;
                }

                dictAdd.Invoke(newDict, new object[] { k, v });
            }

            return newDict;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
