using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Serialization.Native
{
    public interface IEnumerableBinaryPackage : IBinaryPackage
    {
    }

    [Serializable]
    public class EnumerableBinaryPackage : IEnumerableBinaryPackage
    {
        [NonSerialized]
        IBinaryPackageFactory PackageFactory;

        [NonSerialized]
        IEnumerable enumerable;

        string enumerableType;

        IBinaryPackage[] packages;
        private Type EnumerableType => SerializedTypeResolver.ResolveType(
            enumerableType, typeof(Array), typeof(List<>), typeof(HashSet<>));

        public EnumerableBinaryPackage(IEnumerable enumerable, IBinaryPackageFactory packageFactory)
        {
            PackageFactory = packageFactory;
            this.enumerable = enumerable;
            enumerableType = SerializedTypeResolver.Encode(enumerable.GetType());
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

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            PackageFactory = binaryPackageFactory;
            var type = EnumerableType;
            if (typeof(Array).IsAssignableFrom(type))
            {
                return UnpackArray(binaryPackageFactory);
            }
            else if (typeof(List<>) == type.GetGenericTypeDefinition())
            {
                return UnpackList(binaryPackageFactory);
            }
            else if (typeof(HashSet<>) == type.GetGenericTypeDefinition())
            {
                return UnpackList(binaryPackageFactory);
            }

            throw new Exception($"Type {type} not handled");
        }

        private static readonly MethodInfo Cast = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast));
        private static readonly MethodInfo ToArray = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray));
        private object UnpackList(IBinaryPackageFactory binaryPackageFactory)
        {
            var unpackedArray = packages.Select(e => e.Unpack(binaryPackageFactory));
            var type = EnumerableType;
            var cast = Cast.MakeGenericMethod(type.GenericTypeArguments.Single());

            var castedEnumerable = cast.Invoke(null, new object[] { unpackedArray });

            return Activator.CreateInstance(type, new object[] { castedEnumerable });
        }

        private object UnpackArray(IBinaryPackageFactory binaryPackageFactory)
        {
            var unpackedArray = packages.Select(e => e.Unpack(binaryPackageFactory));
            var type = EnumerableType;
            var cast = Cast.MakeGenericMethod(type.GetElementType());

            var castedEnumerable = cast.Invoke(null, new object[] { unpackedArray });

            var toArray = ToArray.MakeGenericMethod(type.GetElementType());

            return toArray.Invoke(null, new object[] { castedEnumerable });
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
