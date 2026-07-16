using GameInterface.Serialization;
using GameInterface.Serialization.Native;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BinaryPackageSecurityTests
{
    [Fact]
    public void Deserialize_UnknownRootType_IsRejected()
    {
        byte[] valid = BinaryPackageSerializer.Serialize(new PrimitiveBinaryPackage(1));

        using var input = new MemoryStream(valid);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        int magic = reader.ReadInt32();
        byte version = reader.ReadByte();
        int rootTypeNameLength = reader.ReadUInt16();
        reader.ReadBytes(rootTypeNameLength);
        int bodyOffset = checked((int)input.Position);

        using var malicious = new MemoryStream();
        using (var writer = new BinaryWriter(malicious, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(magic);
            writer.Write(version);
            byte[] typeName = Encoding.UTF8.GetBytes(typeof(System.Data.DataSet).FullName!);
            writer.Write((ushort)typeName.Length);
            writer.Write(typeName);
            writer.Write(valid, bodyOffset, valid.Length - bodyOffset);
        }

        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Deserialize(malicious.ToArray()));
    }

    [Fact]
    public void Deserialize_OversizedPayload_IsRejectedBeforeParsing()
    {
        var data = new byte[BinaryPackageSerializer.MaxPayloadBytes + 1];

        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Deserialize(data));
    }

    [Fact]
    public void Deserialize_InvalidTypeNameLength_IsRejectedBeforeReadingTypeName()
    {
        byte[] valid = BinaryPackageSerializer.Serialize(new PrimitiveBinaryPackage(1));
        valid[5] = byte.MaxValue;
        valid[6] = byte.MaxValue;

        Assert.Throws<SerializationException>(() => BinaryPackageSerializer.Deserialize(valid));
    }

    [Fact]
    public void PrimitivePackage_SerializableReferenceType_IsRejected()
    {
        Assert.Throws<SerializationException>(() => new PrimitiveBinaryPackage(new Version(1, 0)));
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
