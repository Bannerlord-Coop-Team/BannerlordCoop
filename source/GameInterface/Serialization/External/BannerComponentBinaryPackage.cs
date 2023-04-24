using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for BannerComponent
    /// </summary>
    [Serializable]
    public class BannerComponentBinaryPackage : BinaryPackageBase<BannerComponent>
    {
        public BannerComponentBinaryPackage(BannerComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
