using Common.Extensions;
using GameInterface.Serialization;
using GameInterface;
using System.Reflection;
using System;


namespace GameInterface.Serialization.Internal
{
    /// <summary>
    /// Binary package for CompatibilityInfo
    /// </summary>
    /// 
    [Serializable]
    public class CompatibilityInfoBinaryPackage : BinaryPackageBase<CompatibilityInfo>
    {
        public CompatibilityInfoBinaryPackage(CompatibilityInfo obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}