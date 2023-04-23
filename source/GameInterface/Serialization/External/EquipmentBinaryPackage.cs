using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class EquipmentBinaryPackage : BinaryPackageBase<Equipment>
    {
        public EquipmentBinaryPackage(Equipment obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
