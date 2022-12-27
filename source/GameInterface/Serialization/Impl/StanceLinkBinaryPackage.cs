using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Policy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for StanceLink
    /// </summary>
    [Serializable]
    public class StanceLinkBinaryPackage : BinaryPackageBase<StanceLink>
    {
        public static readonly PropertyInfo StanceLink_Faction1 = typeof(StanceLink).GetProperty(nameof(StanceLink.Faction1));
        public static readonly PropertyInfo StanceLink_Faction2 = typeof(StanceLink).GetProperty(nameof(StanceLink.Faction2));

        private string faction1Id;
        private string faction2Id;
        public StanceLinkBinaryPackage(StanceLink obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<Faction1>k__BackingField",
            "<Faction2>k__BackingField",
        };

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            faction1Id = Object.Faction1?.StringId;
            faction2Id = Object.Faction2?.StringId;
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            SetFaction(StanceLink_Faction1, faction1Id);
            SetFaction(StanceLink_Faction2, faction2Id);
        }

        private void SetFaction(PropertyInfo property, string id)
        {
            if (id == null) return;

            Clan clan = MBObjectManager.Instance.GetObject<Clan>(id);
            property.SetValue(Object, clan);
        }
    }
}
