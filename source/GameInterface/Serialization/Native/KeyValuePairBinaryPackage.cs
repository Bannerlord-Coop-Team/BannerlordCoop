using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class KeyValuePairBinaryPackage : IBinaryPackage
    {
        [NonSerialized]
        private IBinaryPackageFactory binaryPackageFactory;
        [NonSerialized]
        private object Object;
        [NonSerialized]
        private bool IsUnpacked = false;

        private Dictionary<FieldInfo, IBinaryPackage> StoredFields = new Dictionary<FieldInfo, IBinaryPackage>();

        private Type ObjectType;

        public KeyValuePairBinaryPackage(object kvp, IBinaryPackageFactory binaryPackageFactory)
        {
            ObjectType = kvp.GetType();
            Object = kvp;
            this.binaryPackageFactory = binaryPackageFactory;

            if (ObjectType.GetGenericTypeDefinition() != typeof(KeyValuePair<,>)) throw new Exception(
                $"{ObjectType} is not {typeof(KeyValuePair<,>)}");
        }

        public void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, binaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;

            this.binaryPackageFactory = binaryPackageFactory;

            Object = FormatterServices.GetUninitializedObject(ObjectType);

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack(binaryPackageFactory));
            }

            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
