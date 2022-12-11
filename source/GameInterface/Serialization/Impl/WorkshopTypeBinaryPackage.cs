using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for WorkshopType
    /// </summary>
    [Serializable]
    public class WorkshopTypeBinaryPackage : BinaryPackageBase<WorkshopType>
    {
        private string stringId;

        public WorkshopTypeBinaryPackage(WorkshopType obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            stringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<WorkshopType>(stringId);
        }
    }
}
