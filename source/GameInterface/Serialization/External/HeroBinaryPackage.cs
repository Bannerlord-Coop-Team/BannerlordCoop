using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Hero
    /// </summary>
    [Serializable]
    public class HeroBinaryPackage : BinaryPackageBase<Hero>
    {
        private string stringId;
        private string fatherId;
        private string motherId;
        private string[] exSpousesIds;
        private string spouseId;
        private string[] childrenIds;

        public static FieldInfo Hero_ExSpouses => typeof(Hero).GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance);
        public static FieldInfo Hero_Children => typeof(Hero).GetField("_children", BindingFlags.NonPublic | BindingFlags.Instance);
        public HeroBinaryPackage(Hero obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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

            base.PackFields(Excludes);

            fatherId = Object.Father?.StringId;
            motherId = Object.Mother?.StringId;
            spouseId = Object.Spouse?.StringId;

            List<Hero> exSpoueses = Object._exSpouses;
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
            if (string.IsNullOrEmpty(stringId) == false)
            {
                var newObject = ResolveId<Hero>(stringId);
                if (newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            Object.Init();

            // Set the values of all the stored fields on the object
            base.UnpackFields();

            // Set the values of the object's father, mother, spouse, ex-spouses, and children
            Object._father = ResolveId<Hero>(fatherId);
            Object._mother = ResolveId<Hero>(motherId);
            Object._spouse = ResolveId<Hero>(spouseId);

            Hero_ExSpouses.SetValue(Object, new MBList<Hero>(ResolveIds<Hero>(exSpousesIds)));
            Hero_Children.SetValue(Object, new MBList<Hero>(ResolveIds<Hero>(childrenIds)));
        }
    }
}
