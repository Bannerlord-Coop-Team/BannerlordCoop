using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemRosterElementBinaryPackage : BinaryPackageBase<ItemRosterElement>
    {
        public ItemRosterElementBinaryPackage(ItemRosterElement obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
