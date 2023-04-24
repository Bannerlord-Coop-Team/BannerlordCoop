using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class MBBodyPropertyBinaryPackage : BinaryPackageBase<MBBodyProperty>
    {
        public MBBodyPropertyBinaryPackage(MBBodyProperty obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
