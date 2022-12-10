using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class WeaponDesignBinaryPackage : BinaryPackageBase<WeaponDesign>
    {
        public WeaponDesignBinaryPackage(WeaponDesign obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        private static MethodInfo BuildHashedCode = typeof(WeaponDesign).GetMethod("BuildHashedCode", BindingFlags.NonPublic | BindingFlags.Instance);
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
