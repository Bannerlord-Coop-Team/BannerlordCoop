using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class ItemCategoryBinaryPackage : BinaryPackageBase<ItemCategory>
    {
        public string stringId;

        public ItemCategoryBinaryPackage(ItemCategory obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            stringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<ItemCategory>(stringId);
        }
    }
}
