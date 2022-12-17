using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class ClanBinaryPackage : BinaryPackageBase<Clan>
    {
        string stringId;

        public ClanBinaryPackage(Clan obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
            // If the stringId already exists in the object manager use that object
            if (stringId != null)
            {
                var newObject = MBObjectManager.Instance.GetObject<Clan>(stringId);
                if (newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }
        }
    }
}
