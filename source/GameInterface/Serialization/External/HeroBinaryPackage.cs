using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
            // TODO this might give the new player hero an id and the server will try to resolve it
            stringId = ResolveId(Object);

            PackFields(Excludes);

            fatherId = ResolveId(Object.Father);
            motherId = ResolveId(Object.Mother);
            spouseId = ResolveId(Object.Spouse);

            List<Hero> exSpoueses = Object._exSpouses;
            exSpousesIds = ResolveIds(exSpoueses);
            childrenIds = ResolveIds(Object.Children);
        }

        /// <summary>
        /// Unpacks the stored fields and relationships for a hero object.
        /// If the hero object already exists in the object manager, it will be used.
        /// Otherwise, a new hero object will be created and initialized.
        /// </summary>
        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<Hero>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            //Object.Init();

            // Set the values of all the stored fields on the object
            base.UnpackFields();

            // Set the values of the object's father, mother, spouse, ex-spouses, and children
            Object._father = ResolveObject<Hero>(fatherId);
            Object._mother = ResolveObject<Hero>(motherId);
            Object._spouse = ResolveObject<Hero>(spouseId);

            Hero_ExSpouses.SetValue(Object, new MBList<Hero>(ResolveObjects<Hero>(exSpousesIds)));
            Hero_Children.SetValue(Object, new MBList<Hero>(ResolveObjects<Hero>(childrenIds)));
        }
    }
}
