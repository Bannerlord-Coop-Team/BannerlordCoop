using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            var unpackedArray = packages.Select(e => e.Unpack());

            var cast = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast));

            cast = cast.MakeGenericMethod(enumerableType.GenericTypeArguments.Single());

            var castedEnumerable = cast.Invoke(null, new object[] { unpackedArray });

            return Activator.CreateInstance(enumerableType, new object[] { castedEnumerable });
        }

        public T Unpack<T>()
        {
            return (T)Unpack();
        }
    }
}
