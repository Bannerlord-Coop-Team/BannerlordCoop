using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
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

        protected override void PackInternal()
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

            // Resolves _warPartyComponentsCache for Kingdom
            Kingdom kingdom = Object.Clan.Kingdom;
            if (kingdom != null)
            {
                List<WarPartyComponent> kingdomComponents = (List<WarPartyComponent>)KingdomBinaryPackage.Kingdom_WarPartyComponents.GetValue(kingdom);
                if (kingdomComponents.Contains(Object) == false)
                {
                    kingdomComponents.Add(Object);
                }
            }
        }
    }
}
