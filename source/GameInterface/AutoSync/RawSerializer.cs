﻿using ProtoBuf;
using System.IO;

namespace GameInterface.AutoSync;
public class RawSerializer
{
    public static byte[] Serialize(object obj)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            Serializer.Serialize(memoryStream, obj);
            return memoryStream.ToArray();
        }
    }
}
