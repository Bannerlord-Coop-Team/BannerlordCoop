﻿using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class TownBinaryPackage : BinaryPackageBase<Town>
    {
        public string StringId;

        public TownBinaryPackage(Town obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = ResolveId<Town>(StringId);
        }
    }
}
