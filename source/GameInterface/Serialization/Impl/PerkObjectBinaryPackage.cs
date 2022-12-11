using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class PerkObjectBinaryPackage : BinaryPackageBase<PerkObject>
    {
        public string StringId;

        public PerkObjectBinaryPackage(PerkObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            StringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            MBObjectManager.Instance?.GetObject<PerkObject>(StringId);
        }
    }
}
