using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class WeaponDesignElementBinaryPackage : BinaryPackageBase<WeaponDesignElement>
    {
        public WeaponDesignElementBinaryPackage(WeaponDesignElement obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}