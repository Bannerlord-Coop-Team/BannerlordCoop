using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BlowWeaponRecordBinaryPackage : BinaryPackageBase<BlowWeaponRecord>
    {

        public BlowWeaponRecordBinaryPackage(BlowWeaponRecord obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
        
        protected override void PackInternal()
        {
            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
