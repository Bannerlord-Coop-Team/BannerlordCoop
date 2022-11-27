using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            stringId = Object.StringId;
            
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
            
        }

        protected override void UnpackInternal()
        {
            if (stringId != null)
            {
                Object = MBObjectManager.Instance.GetObject<ItemObject>(stringId);
            }
            else
            {
                TypedReference reference = __makeref(Object);
                foreach (FieldInfo field in StoredFields.Keys)
                {
                    field.SetValueDirect(reference, StoredFields[field].Unpack());
                }
            }
        }
    }
}
