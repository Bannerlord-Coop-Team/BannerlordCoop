using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Extensions
{
    public static class BesiegerCampExtensions
    {
        public static string TryGetId(object value, ILogger logger)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
                return null;
            }

            if (!objectManager.TryGetId(value, out string typeId))
            {
                logger.Error("Unable to get ID for instance of type {type}", value.GetType().Name);
                return null;
            }

            return typeId;
        }

        public static byte[] Serialize(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                formatter.Serialize(stream, obj);
                return stream.ToArray();
            }
        }

        public static object Deserialize(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream(data))
            {
                return formatter.Deserialize(stream);
            }
        }

    }
}

//private static bool TryGetTypeId<T>(T value, out string typeId)
//{
//    typeId = null;
//    if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
//    {
//        Logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
//        return false;
//    }

//    if (typeof(T).IsClass)
//    {
//        if (!objectManager.TryGetId(value, out typeId))
//        {
//            Logger.Error("Unable to get ID for instance of type {type}", typeof(T).FullName);
//            return false;
//        }
//        return true;
//    }

//    typeId = value.ToString(); // is this ok?
//    return true;
//}

//string Serialize<T>(T obj) where T: struct
//{

//}

//T Deserialize<T>(T obj) where T: struct
//{

//}