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
    public class ValueTupleBinaryPackage : IBinaryPackage
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ValueTupleBinaryPackage>();

        [NonSerialized]
        private IBinaryPackageFactory binaryPackageFactory;
        [NonSerialized]
        private object Object;
        [NonSerialized]
        private bool IsUnpacked = false;
        
        private Dictionary<string, IBinaryPackage> StoredFields = new Dictionary<string, IBinaryPackage>();

        private string ObjectType;
        protected Type T => SerializedTypeResolver.ResolveType(ObjectType, typeof(ValueTuple<,>));

        public ValueTupleBinaryPackage(object kvp, IBinaryPackageFactory binaryPackageFactory)
        {
            ObjectType = SerializedTypeResolver.Encode(kvp.GetType());
            var type = T;
            Object = kvp;
            this.binaryPackageFactory = binaryPackageFactory;

            if (type.GetGenericTypeDefinition() != typeof(ValueTuple<,>)) throw new Exception(
                $"{type} is not {typeof(ValueTuple<,>)}");
        }

        public void Pack()
        {
            // Iterate through all of the instance fields of the object's type
            var fields = T.GetAllInstanceFields().GroupBy(o => o.Name).Select(g => g.First());
            foreach (FieldInfo field in fields)
            {
                // Get the value of the current field in the object
                // Add a binary package of the field value to the StoredFields collection
                object obj = field.GetValue(Object);
                StoredFields.Add(field.Name, binaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (IsUnpacked) return Object;
            var type = T;
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
