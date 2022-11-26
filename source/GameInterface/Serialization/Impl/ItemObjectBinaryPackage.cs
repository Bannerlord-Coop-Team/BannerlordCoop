using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class ItemObjectBinaryPackage : BinaryPackageBase<ItemObject>
    {

        public string stringId;

        public ItemObjectBinaryPackage(ItemObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                stringId = Object.StringId;
            }
        }

        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<ItemObject>(stringId);
        }
    }
}
