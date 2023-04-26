using Common.Extensions;
using GameInterface.Serialization.Native;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        protected Dictionary<string, IBinaryPackage> StoredFields = new Dictionary<string, IBinaryPackage>();

        string ObjectType;

        public MBReadOnlyListBinaryPackage(object obj, IBinaryPackageFactory binaryPackageFactory)
        {
            BinaryPackageFactory = binaryPackageFactory;
            ObjectType = obj.GetType().AssemblyQualifiedName;
            Object = obj;
        }

        public void Pack()
        {
            var type = Type.GetType(ObjectType);
            foreach (FieldInfo field in type.GetAllInstanceFields().GroupBy(o => o.Name).Select(g => g.First()))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field.Name, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;

            BinaryPackageFactory = binaryPackageFactory;

            IsUnpacked = true;
            var type = Type.GetType(ObjectType);
            Object = FormatterServices.GetUninitializedObject(type);
            var fields = type.GetAllInstanceFields();

            foreach (string fieldName in StoredFields.Keys)
            {
                var field = fields.FirstOrDefault(f => f.Name.Equals(fieldName));

                if (type.IsValueType)
                {
                    object boxed = Object;
                    field.SetValue(boxed, StoredFields[fieldName].Unpack());
                    Object = boxed;
                }
                else
                {
                    field.SetValue(Object, StoredFields[fieldName].Unpack());
                }
            }

            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
