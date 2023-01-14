using Common.Extensions;
using GameInterface.Serialization;
using GameInterface;
using System.Reflection;
using System;


namespace GameInterface.Serialization.Internal
{
    /// <summary>
    /// Binary package for CompatibilityInfo
    /// </summary>
    /// 
    [Serializable]
    public class CompatibilityInfoBinaryPackage : BinaryPackageBase<CompatibilityInfo>
    {
        public CompatibilityInfoBinaryPackage(CompatibilityInfo obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
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
