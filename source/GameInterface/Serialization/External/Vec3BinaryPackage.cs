using System;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Vec3
    /// </summary>
    [Serializable]
    public class Vec3BinaryPackage : BinaryPackageBase<Vec3>
    {
        public Vec3BinaryPackage(Vec3 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
