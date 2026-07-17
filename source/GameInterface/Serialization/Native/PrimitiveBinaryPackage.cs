using System;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    [KnownType(typeof(DateTimeOffset))]
    [KnownType(typeof(TimeSpan))]
    [KnownType(typeof(Guid))]
    public class PrimitiveBinaryPackage : IBinaryPackage
    {
        private object Object;

        public PrimitiveBinaryPackage(object @object)
        {
            if (@object == null) throw new ArgumentNullException(nameof(@object));
            if (!IsSupported(@object.GetType()))
                throw new SerializationException($"Primitive type {@object.GetType().FullName} is not allowed");

            Object = @object;
        }

        public static bool IsSupported(Type type)
        {
            if (type == null || type.IsEnum) return false;
            if (type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid)) return true;

            TypeCode code = Type.GetTypeCode(type);
            return code != TypeCode.Empty && code != TypeCode.Object && code != TypeCode.DBNull;
        }

        public void Pack() { }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (Object == null || !IsSupported(Object.GetType()))
                throw new SerializationException($"Primitive value type {Object?.GetType().FullName ?? "null"} is not allowed");

            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }
    }
}
