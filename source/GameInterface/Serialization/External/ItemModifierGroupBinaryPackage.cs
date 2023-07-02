﻿using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemModifierGroupBinaryPackage : BinaryPackageBase<ItemModifierGroup>
    {
        public ItemModifierGroupBinaryPackage(ItemModifierGroup obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
