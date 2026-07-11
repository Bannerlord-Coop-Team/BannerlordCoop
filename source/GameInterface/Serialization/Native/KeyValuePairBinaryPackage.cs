using Common.Extensions;
using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class KeyValuePairBinaryPackage : IBinaryPackage
    {
        private static readonly ILogger Logger = LogManager.GetLogger<KeyValuePairBinaryPackage>();

        [NonSerialized]
        private IBinaryPackageFactory binaryPackageFactory;
        [NonSerialized]
        private object Object;
        [NonSerialized]
        private bool IsUnpacked = false;
        
        private Dictionary<string, IBinaryPackage> StoredFields = new Dictionary<string, IBinaryPackage>();

        private string ObjectType;
        protected Type T => Type.GetType(ObjectType);

        private static MethodInfo Key = typeof(KeyValuePair<,>).GetMethod("get_Key");

        public KeyValuePairBinaryPackage(object kvp, IBinaryPackageFactory binaryPackageFactory)
        {
            if (kvp.GetType().GetProperty("Key").GetValue(kvp) == null)
            {
                ;
            }

            ObjectType = kvp.GetType().AssemblyQualifiedName;
            var type = Type.GetType(ObjectType);
            Object = kvp;
            this.binaryPackageFactory = binaryPackageFactory;

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
                StoredFields.Add(field.Name, binaryPackageFactory.GetBinaryPackage(obj));
            }

            if (Object.GetType().GetProperty("Key").GetValue(Object) == null)
            {
                ;
            }
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;
            var type = Type.GetType(ObjectType);
            this.binaryPackageFactory = binaryPackageFactory;
            
            Object = FormatterServices.GetUninitializedObject(type);

            UnpackInternal();

            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
        
        private void UnpackInternal()
        {
            var type = Object.GetType();
            var fields = type.GetAllInstanceFields();

            foreach (string fieldName in StoredFields.Keys)
            {
                var field = fields.FirstOrDefault(f => f.Name.Equals(fieldName));

                // Cross-runtime field skew (see BinaryPackageBase.UnpackFields): skip fields the
                // sender's runtime packed that don't exist on this runtime's type.
                if (field == null)
                {
                    Logger.Warning("[FieldSkew] {Type} has no field '{Field}' on this runtime; skipping packed value", type.Name, fieldName);
                    continue;
                }

                field.SetValue((object)Object, StoredFields[fieldName].Unpack(binaryPackageFactory));
            }
        }
    }
}
