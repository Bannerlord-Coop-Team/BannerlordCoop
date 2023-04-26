using System;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class MatrixFrameBinaryPackage : BinaryPackageBase<MatrixFrame>
    {
        public MatrixFrameBinaryPackage(MatrixFrame obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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