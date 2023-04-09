using Common.Extensions;
using GameInterface.Serialization.Native;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Generics
{
    [Serializable]
    public class MBReadOnlyListBinaryPackage : IEnumerableBinaryPackage
    {
        [NonSerialized]
        private bool IsUnpacked = false;

        [NonSerialized]
        object Object;

        [NonSerialized]
        IBinaryPackageFactory BinaryPackageFactory;

        protected Dictionary<FieldInfo, IBinaryPackage> StoredFields = new Dictionary<FieldInfo, IBinaryPackage>();

        Type ObjectType;

        public MBReadOnlyListBinaryPackage(object obj, IBinaryPackageFactory binaryPackageFactory)
        {
            BinaryPackageFactory = binaryPackageFactory;
            ObjectType = obj.GetType();
            Object = obj;
        }

        public void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;

            BinaryPackageFactory = binaryPackageFactory;

            IsUnpacked = true;

            Object = FormatterServices.GetUninitializedObject(ObjectType);

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack(BinaryPackageFactory));
            }

            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
