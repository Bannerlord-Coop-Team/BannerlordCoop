using GameInterface.Serialization;
using GameInterface.Serialization.Native;
using System;
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
            .GetField("TypeName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, typeof(Version).AssemblyQualifiedName);
        typeof(PrimitiveBinaryPackage)
            .GetField("Object", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, null);

        Assert.Throws<SerializationException>(() => package.Unpack(null));
    }

    [Fact]
    public void EnumerablePackage_NonCollectionType_IsRejected()
    {
        var package = new EnumerableBinaryPackage(Array.Empty<int>(), null);
        typeof(EnumerableBinaryPackage)
            .GetField("enumerableType", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(package, typeof(Version).AssemblyQualifiedName);

        Assert.Throws<SerializationException>(() => package.Unpack(null));
    }
}
