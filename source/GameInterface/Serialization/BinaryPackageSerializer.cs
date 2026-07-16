using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace GameInterface.Serialization;

/// <summary>
/// Serializes only the closed set of <see cref="IBinaryPackage"/> implementations.
/// </summary>
public static class BinaryPackageSerializer
{
    private const int Magic = 0x42504353;
    private const byte Version = 1;
    public const int MaxPayloadBytes = 16 * 1024 * 1024;
    private const int MaxItemsInObjectGraph = 2_000_000;
    private const int MaxRootTypeNameLength = 512;

    private static readonly IReadOnlyDictionary<string, Type> PackageTypes = typeof(IBinaryPackage)
        .Assembly
        .GetTypes()
        .Where(type => typeof(IBinaryPackage).IsAssignableFrom(type) &&
                       !type.IsAbstract &&
                       !type.IsInterface &&
                       type.FullName != null)
        .ToDictionary(type => type.FullName, type => type);

    private static readonly Type[] KnownTypes = PackageTypes.Values.ToArray();

    public static byte[] Serialize(object obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (obj is IBinaryPackage == false)
            throw new SerializationException($"Type {obj.GetType().FullName} is not a binary package");

        Type rootType = obj.GetType();
        if (rootType.FullName == null || PackageTypes.ContainsKey(rootType.FullName) == false)
            throw new SerializationException($"Type {rootType.FullName} is not an allowed binary package");

        using (var output = new MemoryStream())
        {
            using (var header = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                header.Write(Magic);
                header.Write(Version);
                byte[] rootTypeName = Encoding.UTF8.GetBytes(rootType.FullName);
                if (rootTypeName.Length > MaxRootTypeNameLength)
                    throw new SerializationException($"Binary package type name exceeded {MaxRootTypeNameLength} bytes");

                header.Write((ushort)rootTypeName.Length);
                header.Write(rootTypeName);
            }

            var serializer = CreateSerializer(rootType);
            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(output, null, null, ownsStream: false))
            {
                serializer.WriteObject(writer, obj);
                writer.Flush();
            }

            if (output.Length > MaxPayloadBytes)
                throw new SerializationException($"Binary package exceeded {MaxPayloadBytes} bytes");

            return output.ToArray();
        }
    }

    public static T Deserialize<T>(byte[] data)
    {
        object package = Deserialize(data);
        if (package is T typedPackage) return typedPackage;

        throw new SerializationException(
            $"Binary package contained {package?.GetType().FullName ?? "null"}, expected {typeof(T).FullName}");
    }

    public static object Deserialize(byte[] data)
    {
        if (data == null) return null;
        if (data.Length < sizeof(int) + sizeof(byte) + sizeof(ushort) || data.Length > MaxPayloadBytes)
            throw new SerializationException("Binary package size was outside the allowed range");

        using (var input = new MemoryStream(data, writable: false))
        using (var header = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
        {
            if (header.ReadInt32() != Magic || header.ReadByte() != Version)
                throw new SerializationException("Binary package header was invalid");

            int rootTypeNameLength = header.ReadUInt16();
            if (rootTypeNameLength == 0 || rootTypeNameLength > MaxRootTypeNameLength ||
                rootTypeNameLength > input.Length - input.Position)
            {
                throw new SerializationException("Binary package type name length was invalid");
            }

            string rootTypeName = Encoding.UTF8.GetString(header.ReadBytes(rootTypeNameLength));
            if (PackageTypes.TryGetValue(rootTypeName, out Type rootType) == false)
            {
                throw new SerializationException($"Binary package type {rootTypeName} is not allowed");
            }

            int offset = checked((int)input.Position);
            int count = data.Length - offset;
            if (count <= 0) throw new SerializationException("Binary package body was empty");

            var quotas = new XmlDictionaryReaderQuotas
            {
                MaxArrayLength = MaxPayloadBytes,
                MaxBytesPerRead = 4096,
                MaxDepth = 128,
                MaxNameTableCharCount = 16 * 1024,
                MaxStringContentLength = MaxPayloadBytes,
            };

            var serializer = CreateSerializer(rootType);
            using (var reader = XmlDictionaryReader.CreateBinaryReader(data, offset, count, quotas))
            {
                object package = serializer.ReadObject(reader, verifyObjectName: true);
                if (package == null || package.GetType() != rootType)
                    throw new SerializationException("Binary package root type did not match its header");

                return package;
            }
        }
    }

    private static DataContractSerializer CreateSerializer(Type rootType)
    {
        return new DataContractSerializer(rootType, new DataContractSerializerSettings
        {
            KnownTypes = KnownTypes,
            MaxItemsInObjectGraph = MaxItemsInObjectGraph,
            PreserveObjectReferences = true,
        });
    }
}
