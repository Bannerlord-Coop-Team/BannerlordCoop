using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class HeroBinaryPackage : BinaryPackageBase<Hero>
    {
        public static readonly FieldInfo Hero_Father = typeof(Hero).GetField("_father", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Hero_Mother = typeof(Hero).GetField("_mother", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Hero_Spouse = typeof(Hero).GetField("_spouse", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Hero_ExSpouses = typeof(Hero).GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Hero_Children = typeof(Hero).GetField("_children", BindingFlags.NonPublic | BindingFlags.Instance);

        private string stringId;
        private string fatherId;
        private string motherId;
        private string[] exSpousesIds;
        private string spouseId;
        private string[] childrenIds;

        public HeroBinaryPackage(Hero obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public static readonly HashSet<string> Excludes = new HashSet<string>
        {
            "_father",
            "_mother",
            "<Issue>k__BackingField",
            "_cachedLastSeenInformation",
            "_exSpouses",
            "ExSpouses",
            "_spouse",
            "_children",
        };

        public override void Pack()
        {
            stringId = Object.StringId;

            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(Excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            fatherId = Object.Father?.StringId;
            motherId = Object.Mother?.StringId;
            spouseId = Object.Spouse?.StringId;

            List<Hero> exSpoueses = (List<Hero>)Hero_ExSpouses.GetValue(Object);
            exSpousesIds = ConvertHeroToId(exSpoueses);
            childrenIds = ConvertHeroToId(Object.Children);
        }

        private string[] ConvertHeroToId<T>(IEnumerable<T> values) where T : MBObjectBase
        {
            if (values == null) return new string[0];

            return values.Select(v => v.StringId).ToArray();
        }

        protected override void UnpackInternal()
        {
            // If the stringId already exists in the object manager use that object
            if(stringId != null)
            {
                var newObject = MBObjectManager.Instance.GetObject<Hero>(stringId);
                if(newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            Object.Init();

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            AssignId(Hero_Father, fatherId);
            AssignId(Hero_Mother, motherId);
            AssignId(Hero_Spouse, spouseId);

            List<Hero> exSpouses = ResolveIds<Hero>(exSpousesIds);
            Hero_ExSpouses.SetValue(Object, exSpouses);
            Hero_Children.SetValue(Object, ResolveIds<Hero>(childrenIds));

            Object.ExSpouses = exSpouses.GetReadOnlyList();
        }

        private List<T> ResolveIds<T>(string[] ids) where T : MBObjectBase
        {
            // Convert ids to instances
            List<T> values = ids.Select(id => MBObjectManager.Instance.GetObject<T>(id)).ToList();

            // Ensure all instances are resolved
            if (values.Any(v => v == null))
                throw new Exception($"Some values were not resolved in {values}");

            return values;
        }

        private void AssignId(FieldInfo fieldInfo, string id)
        {
            if (id == null) return;
            
            Hero hero = MBObjectManager.Instance.GetObject<Hero>(id);
            if (hero == null) throw new Exception($"Hero with id {id} was not found in MBObjectManager");

            fieldInfo.SetValue(Object, hero);
        }
    }
}
