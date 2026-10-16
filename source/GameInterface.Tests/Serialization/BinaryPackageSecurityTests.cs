using GameInterface.Serialization;
using GameInterface.Serialization.Native;
using GameInterface.Services.Villages.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BinaryPackageSecurityTests
{
    [Fact]
    public void Deserialize_MalformedPayload_IsRejected()
    {
        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Deserialize(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Deserialize_OversizedPayload_IsRejectedBeforeParsing()
    {
        var data = new byte[BinaryPackageSerializer.MaxPayloadBytes + 1];

        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Deserialize(data));
    }

    [Fact]
    public void CompressedPackage_RoundTripsAndReducesSize()
    {
        string value = new string('x', 1024 * 1024);
        var package = new PrimitiveBinaryPackage(value);

        byte[] uncompressed = BinaryPackageSerializer.Serialize(package);
        byte[] compressed = BinaryPackageSerializer.SerializeCompressed(package);
        var unpacked = BinaryPackageSerializer
            .DeserializeCompressed<PrimitiveBinaryPackage>(compressed)
            .Unpack(null);

        Assert.True(compressed.Length < uncompressed.Length);
        Assert.Equal(value, unpacked);
    }

    [Fact]
    public void DeserializeCompressed_InvalidHeader_IsRejected()
    {
        Assert.Throws<SerializationException>(() =>
            BinaryPackageSerializer.DeserializeCompressed<PrimitiveBinaryPackage>(new byte[9]));
    }

    [Fact]
    public void DeserializeCompressed_DeclaredSizeOverLimit_IsRejectedBeforeDecompression()
    {
        byte[] data = BinaryPackageSerializer.SerializeCompressed(new PrimitiveBinaryPackage("text"));

        WriteInt32(data, 4, BinaryPackageSerializer.MaxPayloadBytes + 1);

        Assert.Throws<SerializationException>(() =>
            BinaryPackageSerializer.DeserializeCompressed<PrimitiveBinaryPackage>(data));
    }

    [Fact]
    public void DeserializeCompressed_ExpandedSizeMismatch_IsRejected()
    {
        byte[] data = BinaryPackageSerializer.SerializeCompressed(
            new PrimitiveBinaryPackage(new string('x', 1024)));

        WriteInt32(data, 4, 1);

        Assert.Throws<SerializationException>(() =>
            BinaryPackageSerializer.DeserializeCompressed<PrimitiveBinaryPackage>(data));
    }

    [Fact]
    public void DeserializeCompressed_DeclaredSizeLongerThanPayload_IsRejected()
    {
        var package = new PrimitiveBinaryPackage(new string('x', 1024));
        byte[] uncompressed = BinaryPackageSerializer.Serialize(package);
        byte[] data = BinaryPackageSerializer.SerializeCompressed(package);

        WriteInt32(data, 4, uncompressed.Length + 1);

        Assert.Throws<SerializationException>(() =>
            BinaryPackageSerializer.DeserializeCompressed<PrimitiveBinaryPackage>(data));
    }

    [Fact]
    public void PrimitivePackage_TamperedValue_IsRejected()
    {
        var package = new PrimitiveBinaryPackage(1);
        typeof(PrimitiveBinaryPackage)
            .GetField("Object", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, new Version());

        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Serialize(package));
        Assert.Throws<SerializationException>(() => package.Unpack(null));
    }

    [Fact]
    public void EnumPackage_TamperedValue_IsRejected()
    {
        var package = new EnumBinaryPackage(VillageHostileAction.Raid);
        typeof(EnumBinaryPackage)
            .GetField("Object", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, null);
        typeof(EnumBinaryPackage)
            .GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, "Raid");

        Assert.Throws<SerializationException>(() => package.Unpack(null));
    }

    [Fact]
    public void TypeResolver_ValidatesContract()
    {
        Assert.Throws<SerializationException>(() => SerializedTypeResolver.Encode(typeof(List<Version>)));
        Type type = typeof(Dictionary<string, List<int[]>>);
        Assert.Equal(type, SerializedTypeResolver.ResolveType(SerializedTypeResolver.Encode(type)));
        Assert.Throws<SerializationException>(() => new EnumBinaryPackage(TestEnum.Value));
    }

    [Fact]
    public void PrimitivePackage_RoundTripsTypedValues()
    {
        object[] values =
        {
            true, byte.MaxValue, sbyte.MinValue, short.MinValue, ushort.MaxValue, int.MinValue, uint.MaxValue,
            long.MinValue, ulong.MaxValue, 1.25f, 2.5d, decimal.MaxValue, 'x', "text",
            new DateTime(2026, 7, 16, 12, 30, 15, DateTimeKind.Utc),
            new DateTimeOffset(2026, 7, 16, 12, 30, 15, TimeSpan.FromHours(-5)),
            TimeSpan.FromTicks(123456789), Guid.Parse("7312b756-f64e-45c2-9f25-55c8e258b74a"),
        };

        foreach (object value in values)
        {
            byte[] data = BinaryPackageSerializer.Serialize(new PrimitiveBinaryPackage(value));
            var package = BinaryPackageSerializer.Deserialize<PrimitiveBinaryPackage>(data);
            Assert.Equal(value, package.Unpack(null));
        }
    }

    [Theory]
    [InlineData(typeof(bool), "p|Boolean")]
    [InlineData(typeof(byte), "p|Byte")]
    [InlineData(typeof(sbyte), "p|SByte")]
    [InlineData(typeof(short), "p|Int16")]
    [InlineData(typeof(ushort), "p|UInt16")]
    [InlineData(typeof(int), "p|Int32")]
    [InlineData(typeof(uint), "p|UInt32")]
    [InlineData(typeof(long), "p|Int64")]
    [InlineData(typeof(ulong), "p|UInt64")]
    [InlineData(typeof(float), "p|Single")]
    [InlineData(typeof(double), "p|Double")]
    [InlineData(typeof(decimal), "p|Decimal")]
    [InlineData(typeof(char), "p|Char")]
    [InlineData(typeof(string), "p|String")]
    [InlineData(typeof(object), "p|Object")]
    [InlineData(typeof(DateTime), "p|DateTime")]
    [InlineData(typeof(DateTimeOffset), "p|DateTimeOffset")]
    [InlineData(typeof(TimeSpan), "p|TimeSpan")]
    [InlineData(typeof(Guid), "p|Guid")]
    [InlineData(typeof(Tuple<uint, float>), "p|UInt32FloatTuple")]
    [InlineData(typeof(Dictionary<string, object>), "d|p|String|p|Object")]
    [InlineData(typeof(KeyValuePair<string, object>), "k|p|String|p|Object")]
    [InlineData(typeof(Dictionary<string, List<int[]>>), "d|p|String|l|a|p|Int32")]
    public void TypeResolver_FrameworkDescriptors_DoNotDependOnRuntimeAssembly(Type type, string descriptor)
    {
        Assert.Equal(descriptor, SerializedTypeResolver.Encode(type));
        Assert.Equal(type, SerializedTypeResolver.ResolveType(descriptor));
    }

    [Theory]
    [InlineData("p")]
    [InlineData("p|")]
    [InlineData("p|Version")]
    [InlineData("p|System.String")]
    [InlineData("p|String|p|Object")]
    [InlineData("d|p|String|p|Version")]
    public void TypeResolver_InvalidPortableDescriptors_AreRejected(string descriptor)
    {
        Assert.Throws<SerializationException>(() => SerializedTypeResolver.ResolveType(descriptor));
    }

    [Fact]
    public void TypeResolver_PortableTypes_KeepTheCollectionContract()
    {
        Assert.Throws<SerializationException>(() =>
            SerializedTypeResolver.ResolveType("p|String", typeof(Dictionary<,>)));
        Assert.Throws<SerializationException>(() =>
            SerializedTypeResolver.ResolveType("d|p|String|p|Object", typeof(List<>)));
        Assert.Throws<SerializationException>(() => SerializedTypeResolver.Encode(typeof(Tuple<int, float>)));
    }

    [Fact]
    public void SpecializedPackages_RoundTripTypedValues()
    {
        var factory = new BinaryPackageFactory(null);
        var enumPackage = Assert.IsType<EnumBinaryPackage>(factory.GetBinaryPackage(VillageHostileAction.Raid));
        byte[] enumData = BinaryPackageSerializer.Serialize(enumPackage);
        Assert.Equal(VillageHostileAction.Raid,
            BinaryPackageSerializer.Deserialize<EnumBinaryPackage>(enumData).Unpack(null));

        var tuple = new Tuple<uint, float>(7, 1.25f);
        var tuplePackage = Assert.IsType<UInt32FloatTupleBinaryPackage>(factory.GetBinaryPackage(tuple));
        byte[] tupleData = BinaryPackageSerializer.Serialize(tuplePackage);
        Assert.Equal(tuple,
            BinaryPackageSerializer.Deserialize<UInt32FloatTupleBinaryPackage>(tupleData).Unpack(null));
    }

    private enum TestEnum
    {
        Value,
    }

    private static void WriteInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }
}
