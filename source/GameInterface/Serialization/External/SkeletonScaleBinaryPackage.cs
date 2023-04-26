using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for SkeletonScale
    /// </summary>
    [Serializable]
    public class SkeletonScaleBinaryPackage : BinaryPackageBase<SkeletonScale>
    {
        public SkeletonScaleBinaryPackage(SkeletonScale obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
