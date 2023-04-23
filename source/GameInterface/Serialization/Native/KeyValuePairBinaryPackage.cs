using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class KeyValuePairBinaryPackage : IBinaryPackage
    {
        [NonSerialized]
        private readonly BinaryPackageFactory BinaryPackageFactory;
        [NonSerialized]
        private object Object;
        [NonSerialized]
        private bool IsUnpacked = false;
        
        private Dictionary<string, IBinaryPackage> StoredFields = new Dictionary<string, IBinaryPackage>();

        private string ObjectType;
        
        protected Type T => Type.GetType(ObjectType);

        public KeyValuePairBinaryPackage(object kvp, BinaryPackageFactory binaryPackageFactory)
        {
            ObjectType = kvp.GetType().AssemblyQualifiedName;
            var type = Type.GetType(ObjectType);
            Object = kvp;
            BinaryPackageFactory = binaryPackageFactory;

            if (type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>)) throw new Exception(
                $"{ObjectType} is not {typeof(KeyValuePair<,>)}");
        }

        public void Pack()
        {
            // Iterate through all of the instance fields of the object's type
            var fields = Type.GetType(ObjectType).GetAllInstanceFields().GroupBy(o => o.Name).Select(g => g.First());
            foreach (FieldInfo field in fields)
            {
                // Get the value of the current field in the object
                // Add a binary package of the field value to the StoredFields collection
                object obj = field.GetValue(Object);
                StoredFields.Add(field.Name, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        public object Unpack()
        {
            if (IsUnpacked) return Object;
            var type = Type.GetType(ObjectType);
            Object = FormatterServices.GetUninitializedObject(type);

            UnpackInternal();

            return Object;
        }
        
        private void UnpackInternal()
        {
            var type = Object.GetType();
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
        }

        public T Unpack<T>()
        {
            return (T)Object;
        }
    }
}
