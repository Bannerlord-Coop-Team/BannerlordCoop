using GameInterface.Serialization.Generics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Native
{
    public class NativeBinaryPackageCollection
    {
        public static Dictionary<Type, Type> CollectTypes()
        {
            Dictionary<Type, Type> types = new Dictionary<Type, Type>
            {
                { typeof(Array), typeof(EnumerableBinaryPackage) },
                { typeof(List<>), typeof(EnumerableBinaryPackage) },

                { typeof(Dictionary<,>), typeof(DictionaryBinaryPackage) },
                { typeof(KeyValuePair<,>), typeof(KeyValuePairBinaryPackage) },

                { typeof(MBReadOnlyList<>), typeof(MBReadOnlyListBinaryPackage) },
            };

            return types;
        }
    }
}
