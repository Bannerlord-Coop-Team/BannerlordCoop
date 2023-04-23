using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for TroopRosterElement
    /// </summary>
    [Serializable]
    public class TroopRosterElementBinaryPackage : BinaryPackageBase<TroopRosterElement>
    {
        public TroopRosterElementBinaryPackage(TroopRosterElement obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
