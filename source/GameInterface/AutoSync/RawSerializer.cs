using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.IO;

namespace GameInterface.AutoSync;
public class RawSerializer
{
    public static byte[] Serialize(object obj)
    {
        // Null sets reach here; RuntimeTypeModel.Serialize throws on null where the generic call wrote nothing.
        if (obj == null) return Array.Empty<byte>();

        using (MemoryStream memoryStream = new MemoryStream())
        {
            // Serializer.Serialize binds to Serialize<object> here: same bytes, but about 2 KB and several microseconds more per call.
            RuntimeTypeModel.Default.Serialize(memoryStream, obj);
            return memoryStream.ToArray();
        }
    }

    public static T Deserialize<T>(byte[] bytes)
    {
        using (MemoryStream memoryStream = new MemoryStream(bytes))
        {
            return Serializer.Deserialize<T>(memoryStream);
        }
    }
}
