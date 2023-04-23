using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BladeDataBinaryPackage : BinaryPackageBase<BladeData>
    {
        public BladeDataBinaryPackage(BladeData obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
