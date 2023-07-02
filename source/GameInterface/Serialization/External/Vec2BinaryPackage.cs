using System;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Vec2
    /// </summary>
    [Serializable]
    public class Vec2BinaryPackage : BinaryPackageBase<Vec2>
    {
        public Vec2BinaryPackage(Vec2 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
