using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class AttackCollisionDataBinaryPackage : BinaryPackageBase<AttackCollisionData>
    {
        public AttackCollisionDataBinaryPackage(AttackCollisionData obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
