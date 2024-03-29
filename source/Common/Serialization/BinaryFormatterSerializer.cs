﻿using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Common.Serialization
{
    public class BinaryFormatterSerializer
    {
        static readonly BinaryFormatter formatter = new BinaryFormatter();

        public static byte[] Serialize(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] data)
        {
            if (data == default) return default;

            return (T)Deserialize(data);
        }

        public static object Deserialize(byte[] data)
        {
            if (data == null) return null;

            using (MemoryStream ms = new MemoryStream(data))
            {
                return formatter.Deserialize(ms);
            }
        }
    }
}
