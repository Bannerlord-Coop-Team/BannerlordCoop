﻿using System;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for WorkshopType
    /// </summary>
    [Serializable]
    public class WorkshopTypeBinaryPackage : BinaryPackageBase<WorkshopType>
    {
        private string stringId;

        public WorkshopTypeBinaryPackage(WorkshopType obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            stringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Object = ResolveId<WorkshopType>(stringId);
        }
    }
}
