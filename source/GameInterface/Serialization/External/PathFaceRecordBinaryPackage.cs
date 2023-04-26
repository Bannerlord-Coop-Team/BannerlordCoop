using System;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for PathFaceRecord
    /// </summary>
    [Serializable]
    public class PathFaceRecordBinaryPackage : BinaryPackageBase<PathFaceRecord>
    {
        public PathFaceRecordBinaryPackage(PathFaceRecord obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
