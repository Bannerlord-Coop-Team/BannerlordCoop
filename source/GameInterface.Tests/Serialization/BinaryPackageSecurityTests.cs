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
}
