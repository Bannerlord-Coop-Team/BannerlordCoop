using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class EnumerableBinaryPackage : IEnumerableBinaryPackage
    {
        [NonSerialized]
        IBinaryPackageFactory PackageFactory;

        [NonSerialized]
        IEnumerable enumerable;

        Type enumerableType;

        IBinaryPackage[] packages;

        public EnumerableBinaryPackage(IEnumerable enumerable, IBinaryPackageFactory packageFactory)
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
            if (typeof(Array).IsAssignableFrom(enumerableType))
            {
                return UnpackArray();
            }
            else if (typeof(List<>) == enumerableType.GetGenericTypeDefinition())
            {
                return UnpackList();
            }
            else if (typeof(HashSet<>) == enumerableType.GetGenericTypeDefinition())
            {
                return UnpackList();
            }

            throw new Exception($"Type {enumerableType} not handled");
        }


        private static readonly MethodInfo Cast = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast));
        private static readonly MethodInfo ToArray = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray));
        private object UnpackList()
        {
            var unpackedArray = packages.Select(e => e.Unpack());

            var cast = Cast.MakeGenericMethod(enumerableType.GenericTypeArguments.Single());

            var castedEnumerable = cast.Invoke(null, new object[] { unpackedArray });

            return Activator.CreateInstance(enumerableType, new object[] { castedEnumerable });
        }

        private object UnpackArray()
        {
            var unpackedArray = packages.Select(e => e.Unpack());

            var cast = Cast.MakeGenericMethod(enumerableType.GetElementType());

            var castedEnumerable = cast.Invoke(null, new object[] { unpackedArray });

            var toArray = ToArray.MakeGenericMethod(enumerableType.GetElementType());

            return toArray.Invoke(null, new object[] { castedEnumerable });
        }

        public T Unpack<T>()
        {
            return (T)Unpack();
        }
    }
}
