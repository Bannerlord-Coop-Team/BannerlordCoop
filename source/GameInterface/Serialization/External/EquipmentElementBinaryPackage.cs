using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class EquipmentElementBinaryPackage : BinaryPackageBase<EquipmentElement>
    {
        public EquipmentElementBinaryPackage(EquipmentElement obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
