using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class StaticBodyPropertiesBinaryPackage : BinaryPackageBase<StaticBodyProperties>
    {
        public StaticBodyPropertiesBinaryPackage(StaticBodyProperties obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
