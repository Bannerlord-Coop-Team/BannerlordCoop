using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace GameInterface.Serialization.Native
{
    [Serializable]
    public class PrimitiveBinaryPackage : IBinaryPackage
    {
        private enum PrimitiveKind
        {
            Boolean,
            Byte,
            SByte,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Single,
            Double,
            Decimal,
            Char,
            String,
            DateTime,
            DateTimeOffset,
            TimeSpan,
            Guid,
            Enum,
        }

        [NonSerialized]
        private object Object;

        private PrimitiveKind kind;
        private string value;
        private string enumType;

        public PrimitiveBinaryPackage(object @object)
        {
            Object = @object;
            Encode(@object);
        }

        public static bool IsSupported(Type type)
        {
            if (type == null) return false;
            if (type.IsEnum) return true;

            return type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal) ||
                   type == typeof(char) ||
                   type == typeof(string) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid);
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

        private void Encode(object @object)
        {
            if (@object == null) throw new ArgumentNullException(nameof(@object));

            Type type = @object.GetType();
            if (IsSupported(type) == false)
                throw new SerializationException($"Primitive type {type.FullName} is not allowed");

            if (type.IsEnum)
            {
                kind = PrimitiveKind.Enum;
                enumType = type.AssemblyQualifiedName;
                Type underlyingType = Enum.GetUnderlyingType(type);
                object underlyingValue = Convert.ChangeType(@object, underlyingType, CultureInfo.InvariantCulture);
                value = Convert.ToString(underlyingValue, CultureInfo.InvariantCulture);
                return;
            }

            if (type == typeof(bool)) kind = PrimitiveKind.Boolean;
            else if (type == typeof(byte)) kind = PrimitiveKind.Byte;
            else if (type == typeof(sbyte)) kind = PrimitiveKind.SByte;
            else if (type == typeof(short)) kind = PrimitiveKind.Int16;
            else if (type == typeof(ushort)) kind = PrimitiveKind.UInt16;
            else if (type == typeof(int)) kind = PrimitiveKind.Int32;
            else if (type == typeof(uint)) kind = PrimitiveKind.UInt32;
            else if (type == typeof(long)) kind = PrimitiveKind.Int64;
            else if (type == typeof(ulong)) kind = PrimitiveKind.UInt64;
            else if (type == typeof(float)) kind = PrimitiveKind.Single;
            else if (type == typeof(double)) kind = PrimitiveKind.Double;
            else if (type == typeof(decimal)) kind = PrimitiveKind.Decimal;
            else if (type == typeof(char)) kind = PrimitiveKind.Char;
            else if (type == typeof(string)) kind = PrimitiveKind.String;
            else if (type == typeof(DateTime)) kind = PrimitiveKind.DateTime;
            else if (type == typeof(DateTimeOffset)) kind = PrimitiveKind.DateTimeOffset;
            else if (type == typeof(TimeSpan)) kind = PrimitiveKind.TimeSpan;
            else if (type == typeof(Guid)) kind = PrimitiveKind.Guid;

            if (@object is float single) value = single.ToString("R", CultureInfo.InvariantCulture);
            else if (@object is double @double) value = @double.ToString("R", CultureInfo.InvariantCulture);
            else if (@object is DateTime dateTime) value = dateTime.ToBinary().ToString(CultureInfo.InvariantCulture);
            else if (@object is DateTimeOffset dateTimeOffset) value = dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            else if (@object is TimeSpan timeSpan) value = timeSpan.Ticks.ToString(CultureInfo.InvariantCulture);
            else if (@object is char character) value = ((int)character).ToString(CultureInfo.InvariantCulture);
            else if (@object is IFormattable formattable) value = formattable.ToString(null, CultureInfo.InvariantCulture);
            else value = @object.ToString();
        }

        private object Decode()
        {
            switch (kind)
            {
                case PrimitiveKind.Boolean: return bool.Parse(value);
                case PrimitiveKind.Byte: return byte.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.SByte: return sbyte.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Int16: return short.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.UInt16: return ushort.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Int32: return int.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.UInt32: return uint.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Int64: return long.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.UInt64: return ulong.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Single: return float.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Double: return double.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Decimal: return decimal.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.Char: return (char)int.Parse(value, CultureInfo.InvariantCulture);
                case PrimitiveKind.String: return value;
                case PrimitiveKind.DateTime: return DateTime.FromBinary(long.Parse(value, CultureInfo.InvariantCulture));
                case PrimitiveKind.DateTimeOffset: return DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture);
                case PrimitiveKind.TimeSpan: return TimeSpan.FromTicks(long.Parse(value, CultureInfo.InvariantCulture));
                case PrimitiveKind.Guid: return Guid.Parse(value);
                case PrimitiveKind.Enum:
                    Type type = SerializedTypeResolver.ResolveLoadedType(enumType);
                    if (type.IsEnum == false)
                        throw new SerializationException($"Primitive enum type {type.FullName} is not an enum");
                    object underlyingValue = Convert.ChangeType(
                        value,
                        Enum.GetUnderlyingType(type),
                        CultureInfo.InvariantCulture);
                    return Enum.ToObject(type, underlyingValue);
                default:
                    throw new SerializationException($"Primitive kind {kind} is not allowed");
            }
        }
    }
}
