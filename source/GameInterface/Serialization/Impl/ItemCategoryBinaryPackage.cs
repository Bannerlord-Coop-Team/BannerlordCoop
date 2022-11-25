using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class ItemCategoryBinaryPackage : BinaryPackageBase<ItemCategory>
    {
        string stringId;
        public ItemCategoryBinaryPackage(ItemCategory obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            stringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = new ItemCategory(stringId);
        }
    }
}
