using System;
using System.IO;
using ProtoBuf;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

internal static class GenericAcceptFieldsSerializer
{
    public static byte[] Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes ?? Array.Empty<byte>());
        return Serializer.Deserialize<T>(stream);
    }
}
