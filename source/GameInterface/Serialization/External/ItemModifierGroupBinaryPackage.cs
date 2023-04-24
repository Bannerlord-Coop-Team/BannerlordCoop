using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemModifierGroupBinaryPackage : BinaryPackageBase<ItemModifierGroup>
    {
        public ItemModifierGroupBinaryPackage(ItemModifierGroup obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
