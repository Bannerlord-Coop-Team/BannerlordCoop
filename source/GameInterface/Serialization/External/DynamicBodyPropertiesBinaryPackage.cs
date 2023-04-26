using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class DynamicBodyPropertiesBinaryPackage : BinaryPackageBase<DynamicBodyProperties>
    {
        public DynamicBodyPropertiesBinaryPackage(DynamicBodyProperties obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}