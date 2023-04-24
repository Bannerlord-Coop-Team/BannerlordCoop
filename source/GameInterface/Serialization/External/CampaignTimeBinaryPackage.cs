using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CampaignTimeBinaryPackage : BinaryPackageBase<CampaignTime>
    {
        public CampaignTimeBinaryPackage(CampaignTime obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
