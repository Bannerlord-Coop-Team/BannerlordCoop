using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class WeaponComponentBinaryPackage : BinaryPackageBase<WeaponComponent>
    {
        public WeaponComponentBinaryPackage(WeaponComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
    
}