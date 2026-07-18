using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace GameInterface.Serialization;

public static class BinaryPackageSerializer
{
    public const int MaxPayloadBytes = 32 * 1024 * 1024;
    private const int MaxItemsInObjectGraph = 2_000_000;

    private static readonly HashSet<Type> PackageTypes = new HashSet<Type>(
        typeof(IBinaryPackage).Assembly.GetTypes()
        .Where(type => typeof(IBinaryPackage).IsAssignableFrom(type) &&
                       !type.IsAbstract &&
                       !type.IsInterface));

    public static byte[] Serialize(object obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (obj is IBinaryPackage == false || PackageTypes.Contains(obj.GetType()) == false)
            throw new SerializationException($"Type {obj.GetType().FullName} is not an allowed binary package");

        using var output = new MemoryStream();
        using (var writer = XmlDictionaryWriter.CreateBinaryWriter(output, null, null, ownsStream: false))
        {
            CreateSerializer().WriteObject(writer, obj);
            writer.Flush();
        }

        if (output.Length > MaxPayloadBytes)
            throw new SerializationException($"Binary package exceeded {MaxPayloadBytes} bytes");

        return output.ToArray();
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
        if (data.Length == 0 || data.Length > MaxPayloadBytes)
            throw new SerializationException("Binary package size was outside the allowed range");

        using var reader = XmlDictionaryReader.CreateBinaryReader(data, new XmlDictionaryReaderQuotas
        {
            MaxArrayLength = MaxPayloadBytes,
            MaxBytesPerRead = 4096,
            MaxDepth = 128,
            MaxNameTableCharCount = 16 * 1024,
            MaxStringContentLength = MaxPayloadBytes,
        });
        object package = CreateSerializer().ReadObject(reader, verifyObjectName: true);
        if (package is IBinaryPackage == false || PackageTypes.Contains(package.GetType()) == false)
            throw new SerializationException("Binary package root type was not allowed");

        return package;
    }

    private static DataContractSerializer CreateSerializer() =>
        new DataContractSerializer(typeof(IBinaryPackage), new DataContractSerializerSettings
        {
            KnownTypes = PackageTypes,
            MaxItemsInObjectGraph = MaxItemsInObjectGraph,
            PreserveObjectReferences = true,
        });
}
