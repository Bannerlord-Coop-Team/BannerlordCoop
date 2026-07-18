using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class EnumBinaryPackage : IBinaryPackage
    {
        [NonSerialized]
        private object Object;

        private string EnumType;
        private object Value;

        public EnumBinaryPackage(object @object)
        {
            if (@object == null) throw new ArgumentNullException(nameof(@object));
            Type type = @object.GetType();
            if (!type.IsEnum || !SerializedTypeResolver.IsAllowedExactType(type))
                throw new SerializationException($"Enum type {type.FullName} is not allowed");

            Object = @object;
            EnumType = SerializedTypeResolver.Encode(type);
            Value = Convert.ChangeType(@object, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
        }

        public void Pack() { }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (Object != null) return Object;

            Type type = SerializedTypeResolver.ResolveType(EnumType);
            if (!type.IsEnum)
                throw new SerializationException($"Type {type.FullName} is not an enum");

            Type underlyingType = Enum.GetUnderlyingType(type);
            if (Value == null || Value.GetType() != underlyingType)
                throw new SerializationException($"Enum value type must be {underlyingType.FullName}");

            return Object = Enum.ToObject(type, Value);
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
