using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemObjectBinaryPackage : BinaryPackageBase<ItemObject>
    {

        public string stringId;

        public ItemObjectBinaryPackage(ItemObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            stringId = ResolveId(Object);

            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<ItemObject>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            base.UnpackFields();
        }
    }
}
