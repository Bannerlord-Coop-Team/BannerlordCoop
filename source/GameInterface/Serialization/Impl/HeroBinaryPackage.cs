using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class HeroBinaryPackage : BinaryPackageBase<Hero>
    {
        private string stringId;

        public HeroBinaryPackage(Hero obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_father",
            "_mother",
            "<Issue>k__BackingField",
            "_cachedLastSeenInformation",
            "_exSpouses",
            "ExSpouses",
            "Spouses",
            "_children",
        };

        string fatherId;
        string motherId;
        string[] exSpousesIds;
        string spouseId;
        string[] childrenIds;

        public override void Pack()
        {
            stringId = Object.StringId;

            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            fatherId = Object.Father?.StringId;
            motherId = Object.Mother?.StringId;
            spouseId = Object.Spouse?.StringId;

            exSpousesIds = ConvertHeroToId(Object.ExSpouses);
            childrenIds = ConvertHeroToId(Object.Children);
        }

        private string[] ConvertHeroToId<T>(IEnumerable<T> values) where T : MBObjectBase
        {
            if (values == null) return new string[0];

            return values.Select(v => v.StringId).ToArray();
        }

        protected override void UnpackInternal()
        {
            if(stringId != null)
            {
                var newObject = MBObjectManager.Instance.GetObject<Hero>(stringId);
                if(newObject != null)
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

        private Hero ConvertIdToHero(string id)
        {

        }
    }
}
