using GameInterface.Serialization;
using GameInterface.Serialization.Native;
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
    public void PrimitivePackage_TamperedType_IsRejected()
    {
        var package = new PrimitiveBinaryPackage(1);
        typeof(PrimitiveBinaryPackage)
            .GetField("TypeDescriptor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, $"e|{typeof(Version).Assembly.FullName}|{typeof(Version).FullName}");
        typeof(PrimitiveBinaryPackage)
            .GetField("Object", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, null);

        Assert.Throws<SerializationException>(() => package.Unpack(null));
    }

    [Fact]
    public void TypeResolver_ValidatesContract()
    {
        Assert.Throws<SerializationException>(() => SerializedTypeResolver.Encode(typeof(List<Version>)));
        Type type = typeof(Dictionary<string, List<int[]>>);
        Assert.Equal(type, SerializedTypeResolver.ResolveType(SerializedTypeResolver.Encode(type)));
        Assert.Throws<SerializationException>(() => new PrimitiveBinaryPackage(TestEnum.Value));
    }

    private enum TestEnum
    {
        Value,
    }
}
