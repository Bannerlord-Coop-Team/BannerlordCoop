using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BlowWeaponRecordBinaryPackage : BinaryPackageBase<BlowWeaponRecord>
    {

        public BlowWeaponRecordBinaryPackage(BlowWeaponRecord obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
