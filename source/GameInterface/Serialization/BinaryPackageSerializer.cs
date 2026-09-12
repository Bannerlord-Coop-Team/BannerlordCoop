using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace GameInterface.Serialization;

public static class BinaryPackageSerializer
{
    public const int MaxPayloadBytes = 128 * 1024 * 1024;
    private const int CompressedHeaderLength = 8;
    private const int MaxItemsInObjectGraph = 2_000_000;

    private static readonly byte[] CompressedPackageMagic = { (byte)'B', (byte)'P', (byte)'C', 1 };

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

    public static byte[] SerializeCompressed(object obj)
    {
        byte[] serialized = Serialize(obj);

        using var output = new MemoryStream();
        output.Write(CompressedPackageMagic, 0, CompressedPackageMagic.Length);
        WriteInt32(output, serialized.Length);

        using (var compressor = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressor.Write(serialized, 0, serialized.Length);
        }

        byte[] compressed = output.ToArray();
        if (compressed.Length > MaxPayloadBytes)
            throw new SerializationException($"Compressed binary package exceeded {MaxPayloadBytes} bytes");

        return compressed;
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
            MaxDepth = 512,
            MaxNameTableCharCount = 16 * 1024,
            MaxStringContentLength = MaxPayloadBytes,
        });
        object package = CreateSerializer().ReadObject(reader, verifyObjectName: true);
        if (package is IBinaryPackage == false || PackageTypes.Contains(package.GetType()) == false)
            throw new SerializationException("Binary package root type was not allowed");

        return package;
    }

    public static T DeserializeCompressed<T>(byte[] data)
    {
        if (data == null ||
            data.Length <= CompressedHeaderLength ||
            data.Length > MaxPayloadBytes ||
            HasCompressedPackageMagic(data) == false)
        {
            throw new SerializationException("Compressed binary package size or header was invalid");
        }

        int uncompressedLength = ReadInt32(data, CompressedPackageMagic.Length);
        if (uncompressedLength <= 0 || uncompressedLength > MaxPayloadBytes)
            throw new SerializationException("Compressed binary package size was outside the allowed range");

        byte[] serialized = new byte[uncompressedLength];

        try
        {
            using var input = new MemoryStream(
                data,
                CompressedHeaderLength,
                data.Length - CompressedHeaderLength,
                writable: false);
            using var decompressor = new DeflateStream(input, CompressionMode.Decompress);

            int offset = 0;
            while (offset < serialized.Length)
            {
                int bytesRead = decompressor.Read(serialized, offset, serialized.Length - offset);
                if (bytesRead == 0) break;
                offset += bytesRead;
            }

            if (offset != serialized.Length || decompressor.ReadByte() != -1)
                throw new SerializationException("Compressed binary package length did not match its header");
        }
        catch (InvalidDataException ex)
        {
            throw new SerializationException("Compressed binary package payload was invalid", ex);
        }
        catch (IOException ex)
        {
            throw new SerializationException("Compressed binary package could not be read", ex);
        }

        return Deserialize<T>(serialized);
    }

    private static DataContractSerializer CreateSerializer() =>
        new DataContractSerializer(typeof(IBinaryPackage), new DataContractSerializerSettings
        {
            KnownTypes = PackageTypes,
            MaxItemsInObjectGraph = MaxItemsInObjectGraph,
            PreserveObjectReferences = true,
        });

    private static bool HasCompressedPackageMagic(byte[] data)
    {
        for (int i = 0; i < CompressedPackageMagic.Length; i++)
        {
            if (data[i] != CompressedPackageMagic[i]) return false;
        }

        return true;
    }

    private static int ReadInt32(byte[] data, int offset) =>
        data[offset] |
        (data[offset + 1] << 8) |
        (data[offset + 2] << 16) |
        (data[offset + 3] << 24);

    private static void WriteInt32(Stream output, int value)
    {
        output.WriteByte((byte)value);
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)(value >> 16));
        output.WriteByte((byte)(value >> 24));
    }
}
