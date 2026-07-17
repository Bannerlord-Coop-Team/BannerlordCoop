using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class PrimitiveBinaryPackage : IBinaryPackage
    {
        [NonSerialized]
        private object Object;

        private string TypeDescriptor;
        private string Value;
        private string ExtraValue;

        public PrimitiveBinaryPackage(object @object)
        {
            if (@object == null) throw new ArgumentNullException(nameof(@object));
            if (!IsSupported(@object.GetType()))
                throw new SerializationException($"Primitive type {@object.GetType().FullName} is not allowed");

            Object = @object;
            TypeDescriptor = SerializedTypeResolver.Encode(@object.GetType());
            Value = Encode(@object);
        }

        public static bool IsSupported(Type type)
        {
            if (type == null) return false;
            if (type.IsEnum) return SerializedTypeResolver.IsAllowedExactType(type);
            if (type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
                type == typeof(Guid) || type == typeof(Tuple<uint, float>)) return true;

            TypeCode code = Type.GetTypeCode(type);
            return code != TypeCode.Empty && code != TypeCode.Object && code != TypeCode.DBNull;
        }

        public void Pack() { }

        public object Unpack(IBinaryPackageFactory binaryPackageFactory)
        {
            if (Object == null) Object = Decode();
            return Object;
        }

        public T Unpack<T>(IBinaryPackageFactory binaryPackageFactory)
        {
            return (T)Unpack(binaryPackageFactory);
        }

        private string Encode(object value)
        {
            if (value is Tuple<uint, float> tuple)
            {
                ExtraValue = tuple.Item2.ToString("R", CultureInfo.InvariantCulture);
                return tuple.Item1.ToString(CultureInfo.InvariantCulture);
            }
            if (value is DateTime dateTime) return dateTime.ToBinary().ToString(CultureInfo.InvariantCulture);
            if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            if (value is TimeSpan timeSpan) return timeSpan.Ticks.ToString(CultureInfo.InvariantCulture);
            if (value is char character) return ((int)character).ToString(CultureInfo.InvariantCulture);
            if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double @double) return @double.ToString("R", CultureInfo.InvariantCulture);
            if (value.GetType().IsEnum)
                return Convert.ToString(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())), CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private object Decode()
        {
            Type type = SerializedTypeResolver.ResolveType(TypeDescriptor);
            if (!IsSupported(type))
                throw new SerializationException($"Primitive type {type.FullName} is not allowed");

            if (type == typeof(Tuple<uint, float>))
                return new Tuple<uint, float>(uint.Parse(Value, CultureInfo.InvariantCulture),
                    float.Parse(ExtraValue, CultureInfo.InvariantCulture));
            if (type == typeof(DateTime)) return DateTime.FromBinary(long.Parse(Value, CultureInfo.InvariantCulture));
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.ParseExact(Value, "O", CultureInfo.InvariantCulture);
            if (type == typeof(TimeSpan)) return TimeSpan.FromTicks(long.Parse(Value, CultureInfo.InvariantCulture));
            if (type == typeof(Guid)) return Guid.Parse(Value);
            if (type == typeof(char)) return (char)int.Parse(Value, CultureInfo.InvariantCulture);
            if (type.IsEnum)
                return Enum.ToObject(type, Convert.ChangeType(Value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture));

            return Convert.ChangeType(Value, type, CultureInfo.InvariantCulture);
        }
    }
}
