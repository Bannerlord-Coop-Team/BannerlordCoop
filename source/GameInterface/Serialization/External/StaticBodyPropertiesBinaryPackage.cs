using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class StaticBodyPropertiesBinaryPackage : BinaryPackageBase<StaticBodyProperties>
    {
        public StaticBodyPropertiesBinaryPackage(StaticBodyProperties obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
