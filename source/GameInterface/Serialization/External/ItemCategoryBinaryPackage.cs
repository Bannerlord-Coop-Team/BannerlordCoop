using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemCategoryBinaryPackage : BinaryPackageBase<ItemCategory>
    {
        public string stringId;

        public ItemCategoryBinaryPackage(ItemCategory obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            stringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<ItemCategory>(stringId);
        }
    }
}
