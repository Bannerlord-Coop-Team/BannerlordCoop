using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BodyPropertiesBinaryPackage : BinaryPackageBase<BodyProperties>
    {
        public BodyPropertiesBinaryPackage(BodyProperties obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
