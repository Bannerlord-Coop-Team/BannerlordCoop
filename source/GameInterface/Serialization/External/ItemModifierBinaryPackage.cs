using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemModifierBinaryPackage : BinaryPackageBase<ItemModifier>
    {
        public ItemModifierBinaryPackage(ItemModifier obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<Name>k__BackingField"
        };

        protected override void PackInternal()
        {
            base.PackInternal(excludes);
        }
    }
}