using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class MBEquipmentRosterBinaryPackage : BinaryPackageBase<MBEquipmentRoster>
    {
        public MBEquipmentRosterBinaryPackage(MBEquipmentRoster obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
