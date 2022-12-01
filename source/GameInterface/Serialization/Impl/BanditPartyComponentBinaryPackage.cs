using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class BanditPartyComponentBinaryPackage : BinaryPackageBase<BanditPartyComponent>
    {
        public BanditPartyComponentBinaryPackage(BanditPartyComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_cachedName",
        };

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }
        }
    }
}
