using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Hero
    /// </summary>
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
            "_exSpouses",
            "_spouse",
            "_children",
        };

        protected override void PackInternal()
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
            exSpousesIds = PackIds(exSpoueses);
            childrenIds = PackIds(Object.Children);
        }

        /// <summary>
        /// Unpacks the stored fields and relationships for a hero object.
        /// If the hero object already exists in the object manager, it will be used.
        /// Otherwise, a new hero object will be created and initialized.
        /// </summary>
        protected override void UnpackInternal()
        {
            // If the stringId already exists in the object manager use that object
            // Otherwise, create a new object and initialize it
            if (stringId != null)
            {
                var newObject = MBObjectManager.Instance.GetObject<Hero>(stringId);
                if (newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            Object.Init();

            // Set the values of all the stored fields on the object
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            // Set the values of the object's father, mother, spouse, ex-spouses, and children
            Hero_Father.SetValue(Object, ResolveId<Hero>(fatherId));
            Hero_Mother.SetValue(Object, ResolveId<Hero>(motherId));
            Hero_Spouse.SetValue(Object, ResolveId<Hero>(spouseId));

            MBList<Hero> exSpouses = new MBList<Hero>(ResolveIds<Hero>(exSpousesIds));
            Hero_ExSpouses.SetValue(Object, exSpouses);
            Hero_Children.SetValue(Object, ResolveIds<Hero>(childrenIds).ToList());
        }
    }
}
