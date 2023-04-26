using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class WeaponDesignBinaryPackage : BinaryPackageBase<WeaponDesign>
    {
        public WeaponDesignBinaryPackage(WeaponDesign obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        //private static MethodInfo BuildHashedCode = typeof(WeaponDesign).GetMethod("BuildHashedCode", BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
